// One-off Cloud Run job. No model requests; data is isolated from production counters.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { FirestoreStore } from './store.js';

const stores = [new FirestoreStore(process.env.GOOGLE_CLOUD_PROJECT), new FirestoreStore(process.env.GOOGLE_CLOUD_PROJECT)];
const namespace = 'verification-' + randomUUID();
for (const store of stores) store.control = store.db.doc('gameAi/' + namespace);
for (const store of stores) store.deviceCollection = namespace;
const ids = Array.from({ length: 20 }, () => randomUUID());
const device = i => `kiosk${String(i + 1).padStart(2, '0')}`;
const now = Date.parse('2027-01-07T02:00:00Z');
try {
  const started = Date.now();
  const results = await Promise.allSettled(ids.map((requestId, i) =>
    stores[i % 2].begin(device(i), { requestId, fingerprint: 'verification' }, now)));
  const successes = results.filter(r => r.status === 'fulfilled').length;
  console.log(JSON.stringify({ test: 'distributed_admissions', successes, expected: 20, durationMs: Date.now() - started }));
  for (const result of results) if (result.status === 'rejected') console.error(result.reason);
  assert.equal(successes, 20);
  const refs = ids.map((_, i) => stores[0].counterRef('event', device(i)));
  let docs = await stores[0].db.getAll(...refs);
  assert.equal(docs.reduce((sum, doc) => sum + doc.data().counts.eventTotal, 0), 20);
  assert.equal(docs.reduce((sum, doc) => sum + Object.keys(doc.data().leases).length, 0), 20);
  await assert.rejects(() => stores[1].begin(device(0), { requestId: randomUUID(), fingerprint: 'verification' }, now), /device_busy/);
  await assert.rejects(() => stores[1].begin('kiosk21', { requestId: randomUUID(), fingerprint: 'verification' }, now), /unknown_device/);
  await Promise.all(ids.map((requestId, i) => stores[i % 2].finish(requestId, { type: 'error', code: 'verification_only' }, { phase: 'event', device: device(i) })));
  docs = await stores[0].db.getAll(...refs);
  assert.equal(docs.reduce((sum, doc) => sum + Object.keys(doc.data().leases).length, 0), 0);
  console.log('PASS: two independent clients admitted 20 event players, rejected a busy kiosk and an unregistered 21st, and released every lease. No model was called.');
} finally {
  const batch = stores[0].db.batch(); batch.delete(stores[0].control);
  for (const requestId of ids) batch.delete(stores[0].db.doc('gameAiRequests/' + requestId));
  for (let i = 0; i < 20; i++) batch.delete(stores[0].counterRef('event', device(i)));
  await batch.commit();
  await Promise.all(stores.map(store => store.db.terminate()));
}
