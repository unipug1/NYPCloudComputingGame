import { Firestore } from '@google-cloud/firestore';
import { admit, emptyState, reserve, phaseAt, GameError } from './policy.js';

export class FirestoreStore {
  constructor(projectId, databaseId = '(default)') {
    this.db = new Firestore({ projectId, databaseId });
    this.control = this.db.doc('gameAi/control');
    this.deviceCollection = 'gameAiDevices';
  }

  counterRef(phase, device) {
    if (phase !== 'event') return this.control;
    // Exactly twenty provisioned physical computers. One independent lease and
    // 1/20 of the event allowance each avoids a hot global write document.
    if (!/^kiosk(0[1-9]|1[0-9]|20)$/.test(device)) throw new GameError('unknown_device', 403);
    return this.db.doc(`${this.deviceCollection}/${device}`);
  }

  async begin(device, request, now) {
    const requestRef = this.db.doc(`gameAiRequests/${request.requestId}`);
    const { phase } = phaseAt(now);
    const counter = this.counterRef(phase, device);
    return this.db.runTransaction(async tx => {
      const [control, record, global = control] = await tx.getAll(...(phase === 'event'
        ? [counter, requestRef, this.control] : [counter, requestRef]));
      const state = { ...emptyState(), ...control.data(), requests: {} };
      if (state.disabled || global.data()?.disabled) throw new GameError('service_paused', 503);
      if (record.exists) state.requests[request.requestId] = record.data();
      const result = admit(state, { device, request, now, quotaSlots: phase === 'event' ? 20 : 1 });
      if (!result.cached) {
        const requestData = state.requests[request.requestId];
        delete state.requests;
        tx.set(counter, state);
        // No user message or full conversation is persisted here.
        tx.create(requestRef, { ...requestData, createdAt: new Date(now) });
      }
      return result;
    }, { maxAttempts: 8 });
  }

  async fallback(phase, day, requestId, now = Date.now(), device) {
    const counter = this.counterRef(phase, device);
    await this.db.runTransaction(async tx => {
      const [doc, global = doc] = await tx.getAll(...(phase === 'event' ? [counter, this.control] : [counter]));
      const state = { ...emptyState(), ...doc.data() };
      if (state.disabled || global.data()?.disabled || !state.leases[requestId] || state.leases[requestId].expires <= now)
        throw new GameError('service_paused', 503);
      reserve(state, phase, day, phase === 'event' ? 20 : 1);
      delete state.requests;
      tx.set(counter, state);
    }, { maxAttempts: 8 });
  }

  async finish(requestId, result, { phase = 'test', device } = {}) {
    const counter = this.counterRef(phase, device);
    await this.db.runTransaction(async tx => {
      const doc = await tx.get(counter);
      const state = { ...emptyState(), ...doc.data() };
      delete state.leases[requestId];
      delete state.requests;
      tx.set(counter, state);
      tx.set(this.db.doc(`gameAiRequests/${requestId}`), { result }, { merge: true });
    }, { maxAttempts: 8 });
  }
}

// Only used by tests; production always uses Firestore and fails closed on errors.
export class MemoryStore {
  state = emptyState();
  async begin(device, request, now) { return admit(this.state, { device, request, now }); }
  async fallback(phase, day) { reserve(this.state, phase, day); }
  async finish(requestId, result) {
    delete this.state.leases[requestId];
    this.state.requests[requestId].result = result;
  }
}
