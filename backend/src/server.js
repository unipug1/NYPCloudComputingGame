import { createServer } from 'node:http';
import { FirestoreStore } from './store.js';
import { makeProviders } from './providers.js';
import { createApp } from './app.js';

const config = {
  projectId: process.env.GOOGLE_CLOUD_PROJECT,
  vertexModel: process.env.VERTEX_MODEL || 'gemini-3.5-flash-lite',
  openRouterKey: process.env.OPENROUTER_API_KEY === 'NOT_CONFIGURED' ? '' : (process.env.OPENROUTER_API_KEY || ''),
  openRouterModel: process.env.OPENROUTER_MODEL || 'nvidia/nemotron-3.5-lightning:free',
  deviceKeys: JSON.parse(process.env.GAME_DEVICE_KEYS || '{}'),
  allowedOrigins: (process.env.ALLOWED_ORIGINS || '').split(',').filter(Boolean),
  paidAiEnabled: process.env.PAID_AI_ENABLED === 'true',
};
if (!config.projectId) throw new Error('GOOGLE_CLOUD_PROJECT is required');
const server = createServer(createApp({ config,
  store: new FirestoreStore(config.projectId, process.env.FIRESTORE_DATABASE || '(default)'),
  providers: makeProviders(config),
}));
server.requestTimeout = 40000;
server.headersTimeout = 10000;
server.listen(Number(process.env.PORT || 8080), '0.0.0.0');
process.on('SIGTERM', () => server.close());
