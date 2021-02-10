import { FileOptions } from 'discord.js';
import BaseSite from './BaseSite';
import ProcessResponse from './ProcessResponse';
import path from 'path';
import Environment from '../Environment';
import os from 'os';
import AdmZip from 'adm-zip';
import fs from 'fs/promises';
import rimraf from 'rimraf';
import ffmpeg from 'fluent-ffmpeg';
import { MAX_FILESIZE } from '../Constants';
import PixivWeb from 'pixiv-web-api';
import { IllustDetailsResponse } from 'pixiv-web-api/dist/ResponseTypes';

class Pixiv extends BaseSite {
    name = 'Pixiv';

    pattern = /https?:\/\/(www\.)?pixiv.net\/.*artworks\/(?<id>\d+)/i;

    api: PixivWeb;

    constructor() {
        super();

        this.api = new PixivWeb({
            username: Environment.get('PIXIV_LOGIN') as string,
            password: Environment.get('PIXIV_PASSWORD') as string,
            cookie: Environment.get('PIXIV_COOKIE') as string,
        });
    }

    async process(match: RegExpMatchArray): Promise<ProcessResponse | false> {
        await this.api.login();

        const id = parseInt(match.groups.id);

        const response = await this.api.illustDetails(id);

        if (response.body.illustType == 2) {
            return this.processUgoira(response);
        }

        return this.processImage(response);
    }

    async processImage(
        details: IllustDetailsResponse
    ): Promise<ProcessResponse | false> {
        const message: ProcessResponse = {
            embeds: [],
            files: [],
        };

        const pageCount = details.body.pageCount;

        if (pageCount == 1) {
            const urls = [
                details.body.urls.original,
                details.body.urls.regular,
                details.body.urls.small,
            ];

            const url = await this.determineHighestQuality(urls);

            if (!url) {
                return Promise.resolve(message);
            }

            const file = await this.getFile(url);

            message.files.push(file);

            return Promise.resolve(message);
        }

        const pagesDetails = await this.api.illustPages(details.body.id);

        const postLimit = Environment.get('PIXIV_POST_LIMIT', 5) as number;

        const pages = pagesDetails.body.slice(0, postLimit);

        for (const page of pages) {
            const urls = [
                page.urls.original,
                page.urls.regular,
                page.urls.small,
            ];

            const url = await this.determineHighestQuality(urls);

            if (!url) {
                continue;
            }

            const file = await this.getFile(url);

            message.files.push(file);
        }

        if (pageCount > postLimit) {
            message.text = `This is part of a ${pageCount} image set.`;
        }

        return Promise.resolve(message);
    }

    async processUgoira(
        details: IllustDetailsResponse
    ): Promise<ProcessResponse | false> {
        const message: ProcessResponse = {
            embeds: [],
            files: [],
        };

        const metadata = await this.api.ugoiraMetaData(details.body.id);

        const file = await this.getFile(metadata.body.originalSrc);

        // Because the attachment can be a stirng or buffer, we have to type cast to any, as string can't go to buffer automatically
        const buffer = file.attachment as Buffer;

        const zip = new AdmZip(buffer);

        const basePath = path.join(os.tmpdir(), details.body.id.toString());

        const concatFilePath = path.join(basePath, 'ffconcat');

        const format = Environment.get('PIXIV_UGOIRA_FORMAT', 'mp4') as string;

        const videoFilePath = path.join(basePath, `ugoira.${format}`);

        zip.extractAllTo(basePath, true);

        const ffconcat = this.buildConcatFile(metadata.body.frames);

        await fs.writeFile(concatFilePath, ffconcat);

        await this.ffmpeg(concatFilePath, videoFilePath);

        const video = await fs.readFile(videoFilePath);

        // Remove all files in the temporary directory
        rimraf(basePath, (err) => {
            if (err) {
                console.error(err);
            }
        });

        // Snake case and remove hyphens from title
        const fileName = `${details.body.title
            .toLowerCase()
            .replace('-', '')
            .replace(/\s+/g, '_')}_ugoira.${format}`;

        message.files.push({
            attachment: video,
            name: fileName,
        });

        return Promise.resolve(message);
    }

    buildConcatFile(frames: Array<{ file: string; delay: number }>): string {
        let concat = '';

        for (const frame of frames) {
            const delay = frame.delay / 1000;

            concat += `file ${frame.file}\n`;
            concat += `duration ${delay}\n`;
        }

        const lastFrame = frames[frames.length - 1];

        concat += `file ${lastFrame.file}\n`;

        return concat;
    }

    async ffmpeg(input: string, output: string): Promise<boolean> {
        // This is required as fluent-ffmpeg doesn't support promises unfortunately
        return new Promise<boolean>((resolve, reject) => {
            ffmpeg({ cwd: path.dirname(input) })
                .input(input)
                .inputFormat('concat')
                .videoBitrate(
                    Environment.get('PIXIV_UGOIRA_BITRATE', 2000) as number
                )
                .on('error', (err) => reject(err))
                .on('end', () => resolve(true))
                .save(output);
        });
    }

    /**
     * Determines the highest quality of an image that can be posted to Discord inside of its size limit
     *
     * @param urls a list of urls from highest quality to lowest quality
     */
    async determineHighestQuality(urls: string[]): Promise<string | false> {
        for (const url of urls) {
            const response = await this.api.pokeFile(url);

            if (parseInt(response.headers['content-length']) < MAX_FILESIZE) {
                return Promise.resolve(url);
            }
        }

        return Promise.resolve(false);
    }

    async getFile(url: string): Promise<FileOptions> {
        const response = await this.api.getFile(url);

        const file: FileOptions = {
            attachment: response,
            name: path.basename(new URL(url).pathname),
        };

        return Promise.resolve(file);
    }
}

export default Pixiv;
