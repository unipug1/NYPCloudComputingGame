import { createHash, timingSafeEqual } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { LIMITS, GameError, validateTurn } from './policy.js';

const systemPrompt = readFileSync(new URL('./system-prompt.txt', import.meta.url), 'utf8');
const digest = value => createHash('sha256').update(value).digest();

export function createApp({ config, store, providers, now = Date.now, log = console.log }) {
  const deviceKeys = Object.entries(config.deviceKeys).map(([device, key]) => ({ device, hash: digest(key) }));
  if (!deviceKeys.length || deviceKeys.length > 20 || deviceKeys.some(entry => !/^kiosk(0[1-9]|1[0-9]|20)$/.test(entry.device)) ||
      Object.values(config.deviceKeys).some(key => typeof key !== 'string' || key.length < 32))
    throw new Error('Configure between 1 and 20 random device keys of at least 32 characters');
  return async (req, res) => {
    const origin = req.headers.origin;
    if (origin && config.allowedOrigins.includes(origin)) {
      res.setHeader('Access-Control-Allow-Origin', origin); res.setHeader('Vary', 'Origin');
    }
    if (req.method === 'OPTIONS') {
      res.writeHead(origin && config.allowedOrigins.includes(origin) ? 204 : 403, {
        'Access-Control-Allow-Methods': 'POST, GET, OPTIONS',
        'Access-Control-Allow-Headers': 'Content-Type, X-Game-Key', 'Access-Control-Max-Age': '600',
      }); res.end(); return;
    }
    if (req.method === 'GET' && req.url === '/health') {
      res.writeHead(200, { 'Content-Type': 'application/json' }); res.end('{"ok":true}'); return;
    }
    let admitted = false, requestId, outcome, admissionContext;
    const abort = new AbortController();
    const disconnect = () => { if (!res.writableEnded) abort.abort(); };
    res.on('close', disconnect);
    const emit = event => { if (!res.destroyed) res.write(JSON.stringify(event) + '\n'); };
    const started = now();
    try {
      if (req.method !== 'POST' || req.url !== '/v1/turn') throw new GameError('not_found', 404);
      if (origin && !config.allowedOrigins.includes(origin)) throw new GameError('origin_denied', 403);
      const supplied = String(req.headers['x-game-key'] || '');
      if (supplied.length > 200) throw new GameError('unauthorized', 401);
      const hashed = digest(supplied);
      const device = deviceKeys.find(entry => timingSafeEqual(entry.hash, hashed))?.device;
      if (!device) throw new GameError('unauthorized', 401);
      if (!/^application\/json(?:;|$)/i.test(req.headers['content-type'] || '')) throw new GameError('content_type', 415);
      const chunks = []; let size = 0;
      for await (const chunk of req) {
        size += chunk.length;
        if (size > 20000) throw new GameError('request_too_large', 413);
        chunks.push(chunk);
      }
      let input;
      try { input = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { throw new GameError('invalid_json', 400); }
      const turn = validateTurn(input);
      requestId = turn.requestId;
      const userText = `[Current Trust: ${turn.currentTrust}/100 | Previous Topic: ${JSON.stringify(turn.lockedReason || 'NONE')}]\nVisitor: ${JSON.stringify(turn.playerText)}`;
      const messages = [...turn.history, { role: 'user', content: userText }];
      if (Buffer.byteLength(systemPrompt) + Buffer.byteLength(JSON.stringify(messages)) > LIMITS.maxPromptBytes)
        throw new GameError('context_too_large', 400);
      if (!config.paidAiEnabled) throw new GameError('credit_verification_required', 503);
      const fingerprint = createHash('sha256').update(JSON.stringify(turn)).digest('hex');
      const admission = await store.begin(device, { ...turn, fingerprint }, now());
      admissionContext = { phase: admission.phase, device };
      res.writeHead(200, { 'Content-Type': 'application/x-ndjson; charset=utf-8',
        'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
      res.flushHeaders();
      if (admission.cached) { emit(admission.cached); return; }
      admitted = true;
      const deadline = AbortSignal.timeout(32000);
      for (const provider of ['vertex', 'openrouter']) {
        if (abort.signal.aborted || deadline.aborted) throw new GameError('cancelled', 499);
        if (provider === 'openrouter') {
          if (!config.openRouterKey) break;
          // A backup is another provider attempt and consumes another reserved slot.
          await store.fallback(admission.phase, admission.day, requestId, now(), device);
          emit({ type: 'reset' });
        }
        const signal = AbortSignal.any([abort.signal, deadline, AbortSignal.timeout(14000)]);
        let result;
        try {
          result = await providers[provider](messages, systemPrompt, signal, emit);
        } catch (error) {
          log(JSON.stringify({ requestId, provider, code: error.code || error.name || 'provider_failure' }));
          continue;
        }
        // Apply the gameplay rule even if a model ignores its instruction.
        if (!turn.history.length) { result.response.isDeadEnd = false; result.response.deadEndReason = ''; }
        outcome = { type: 'result', response: result.response, provider };
        await store.finish(requestId, outcome, admissionContext); admitted = false;
        emit(outcome);
        log(JSON.stringify({ requestId, provider, durationMs: now() - started, usage: result.usage }));
        return;
      }
      throw new GameError('ai_temporarily_unavailable', 503);
    } catch (error) {
      outcome = { type: 'error', code: error instanceof GameError ? error.code : 'service_unavailable' };
      if (!res.headersSent) {
        res.writeHead(error instanceof GameError ? error.status : 503, {
          'Content-Type': 'application/x-ndjson', 'Cache-Control': 'no-store', 'Retry-After': '3',
        });
      }
      emit(outcome);
    } finally {
      if (admitted) await store.finish(requestId, outcome || { type: 'error', code: 'cancelled' }, admissionContext).catch(() => {});
      res.off('close', disconnect);
      res.end();
    }
  };
}
