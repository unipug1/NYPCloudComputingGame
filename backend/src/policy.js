export class GameError extends Error {
  constructor(code, status = 429) { super(code); this.code = code; this.status = status; }
}

export const LIMITS = Object.freeze({
  maxActive: 20, minIntervalMs: 2000, leaseMs: 45000,
  testDaily: 100, testTotal: 500, eventDaily: 6000, eventTotal: 18000,
  maxPromptBytes: 12000, maxOutputTokens: 512, attemptReserveUsd: 0.01,
});

// Open House takes place in Singapore. Event allowance cannot be spent early.
export function phaseAt(now) {
  const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Singapore',
    year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(now));
  if (day > '2027-01-09') throw new GameError('event_finished', 403);
  return { day, phase: day >= '2027-01-07' ? 'event' : 'test' };
}

export function validateTurn(input) {
  const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
  if (!input || !uuid.test(input.requestId) || !uuid.test(input.sessionId) ||
      typeof input.playerText !== 'string' || !input.playerText.trim() || input.playerText.length > 400 ||
      !Number.isFinite(input.currentTrust) || input.currentTrust < 0 || input.currentTrust > 100 ||
      typeof input.lockedReason !== 'string' || input.lockedReason.length > 80 ||
      !Array.isArray(input.history) || input.history.length > 8) throw new GameError('invalid_request', 400);
  let previousRole;
  for (const item of input.history) {
    if (!item || !['user', 'assistant'].includes(item.role) || typeof item.content !== 'string' ||
        item.content.length > 2000 || item.role === previousRole) throw new GameError('invalid_history', 400);
    previousRole = item.role;
  }
  if (previousRole && previousRole !== 'assistant') throw new GameError('invalid_history', 400);
  return { requestId: input.requestId, sessionId: input.sessionId, playerText: input.playerText.trim(),
    currentTrust: Math.round(input.currentTrust), lockedReason: input.lockedReason,
    history: input.history.map(({ role, content }) => ({ role, content })) };
}

// Pure state transitions shared by Firestore and the concurrent test store.
export function admit(state, { device, request, now, quotaSlots = 1 }) {
  const { phase, day } = phaseAt(now);
  const cached = state.requests[request.requestId];
  if (cached) {
    if (cached.device !== device || cached.fingerprint !== request.fingerprint)
      throw new GameError('request_id_conflict', 409);
    if (cached.result) return { cached: cached.result };
    throw new GameError('request_in_progress', 409);
  }
  const leases = Object.fromEntries(Object.entries(state.leases).filter(([, v]) => v.expires > now));
  if (Object.keys(leases).length >= LIMITS.maxActive) throw new GameError('busy');
  if (Object.values(leases).some(v => v.device === device)) throw new GameError('device_busy');
  const last = state.devices[device] || 0;
  if (now - last < LIMITS.minIntervalMs) throw new GameError('slow_down');
  // Charge an allowance before the first network call, even if it fails or disconnects.
  reserve(state, phase, day, quotaSlots);
  state.devices[device] = now;
  state.leases = leases;
  state.leases[request.requestId] = { device, expires: now + LIMITS.leaseMs };
  state.requests[request.requestId] = { device, fingerprint: request.fingerprint };
  return { phase, day };
}

export function reserve(state, phase, day, quotaSlots = 1) {
  const totalKey = `${phase}Total`, dailyKey = `${phase}:${day}`;
  const totalLimit = (phase === 'test' ? LIMITS.testTotal : LIMITS.eventTotal) / quotaSlots;
  const dailyLimit = (phase === 'test' ? LIMITS.testDaily : LIMITS.eventDaily) / quotaSlots;
  if ((state.counts[totalKey] || 0) >= totalLimit || (state.counts[dailyKey] || 0) >= dailyLimit)
    throw new GameError('usage_limit');
  state.counts[totalKey] = (state.counts[totalKey] || 0) + 1;
  state.counts[dailyKey] = (state.counts[dailyKey] || 0) + 1;
}

export function emptyState() { return { counts: {}, devices: {}, leases: {}, requests: {} }; }
