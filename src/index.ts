import { ShardingManager } from 'discord.js';
import dotenv from 'dotenv';
import { join } from 'path';
import Environment from './Environment';
import Logger from './Logger';
import * as Sentry from '@sentry/node';

dotenv.config();

Sentry.init({
    integrations: [new Sentry.Integrations.Http()],
    initialScope: {
        contexts: {
            shard: {
                identifier: 'master',
                master: true,
            },
        },
    },
});

const manager = new ShardingManager(join(__dirname, 'bot.js'), {
    token: Environment.get('DISCORD_API_KEY') as string,
});

manager.on('shardCreate', (shard) =>
    Logger.info(`Launched Shard ${shard.id}`, 'Manager')
);
manager.spawn();
