import BaseSite from './BaseSite';
import ProcessResponse from './ProcessResponse';
import got from 'got';
import { version } from '../../package.json';
import Environment from '../Environment';
import path from 'path';
import { MessageEmbed } from 'discord.js';
import { DateTime } from 'luxon';

class ArtStation extends BaseSite {
    name = 'ArtStation';

    pattern = /https?:\/\/(www\.)?artstation.com\/artwork\/(?<hash>\S*)/i;

    async process(match: RegExpMatchArray): Promise<ProcessResponse | false> {
        const message: ProcessResponse = {
            embeds: [],
            files: [],
        };

        /* eslint-disable  @typescript-eslint/no-explicit-any */
        const response: Record<string, any> = await got
            .get(
                `https://www.artstation.com/projects/${match.groups.hash}.json`,
                {
                    responseType: 'json',
                    headers: {
                        'User-Agent': `SaucyBot/${version}`,
                        Referer: 'https://www.artstation.com/',
                    },
                }
            )
            .json();

        // Discord embeds the first ArtStation item, so if there's only one, ignore the request
        if (response.assets.length == 1) {
            return Promise.resolve(false);
        }

        const limit = Environment.get('ARTSTATION_POST_LIMIT', 5);

        if (response.assets.length > limit) {
            message.text = `This is part of a ${response.assets.length} image set.`;
        }

        const coverFileName = path.basename(
            new URL(response.cover_url).pathname
        );

        const assets = response.assets.slice(1, limit);

        for (const asset of assets) {
            // If this asset isn't an image, skip over it as we can only display those for now
            if (!['image', 'cover'].includes(asset.asset_type)) {
                continue;
            }

            // If this is the same as the cover, skip it.
            if (asset.image_url.includes(coverFileName)) {
                continue;
            }

            const embed = new MessageEmbed({
                type: 'image',
                title: asset.title ? asset.title : response.title,
                url: response.permalink,
                color: this.color,
                timestamp: DateTime.fromISO(response.published_at)
                    .toUTC()
                    .toMillis(),
                image: {
                    url: asset.image_url,
                },
                author: {
                    name: response.user.full_name,
                    url: response.user.permalink,
                    icon_url: response.user.medium_avatar_url,
                },
                fields: [
                    {
                        name: 'Views',
                        value: response.views_count,
                        inline: true,
                    },
                    {
                        name: 'Likes',
                        value: response.likes_count,
                        inline: true,
                    },
                ],
            });

            message.embeds.push(embed);
        }

        return Promise.resolve(message);
    }
}

export default ArtStation;
