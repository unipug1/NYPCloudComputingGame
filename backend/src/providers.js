import { GoogleAuth } from 'google-auth-library';
import { LIMITS, GameError } from './policy.js';
import { RESPONSE_SCHEMA, partialDialogue, sseData, validateResponse } from './protocol.js';

export function makeProviders(config, { fetchImpl = fetch, auth = new GoogleAuth({ scopes: ['https://www.googleapis.com/auth/cloud-platform'] }) } = {}) {
  async function consume(response, provider, emit) {
    if (!response.ok || !response.body) throw new GameError(`provider_http_${response.status}`, 502);
    let text = '', shown = '', complete = false, usage = {};
    for await (const data of sseData(response.body)) {
      if (data === '[DONE]') { complete = true; break; }
      const frame = JSON.parse(data);
      if (frame.error) throw new GameError('provider_error', 502);
      if (provider === 'vertex') {
        const candidate = frame.candidates?.[0];
        for (const part of candidate?.content?.parts || []) if (part.text && !part.thought) text += part.text;
        if (candidate?.finishReason) {
          if (candidate.finishReason !== 'STOP') throw new GameError('provider_incomplete', 502);
          complete = true;
        }
        usage = frame.usageMetadata || usage;
      } else {
        text += frame.choices?.[0]?.delta?.content || '';
        const finish = frame.choices?.[0]?.finish_reason;
        if (finish && finish !== 'stop') throw new GameError('provider_incomplete', 502);
        usage = frame.usage || usage;
      }
      if (text.length > 8192) throw new GameError('provider_output_too_large', 502);
      const current = partialDialogue(text);
      if (current && current !== shown) { emit({ type: 'dialogue', text: current }); shown = current; }
    }
    if (!complete) throw new GameError('provider_incomplete', 502);
    return { response: validateResponse(text), usage };
  }

  return {
    async vertex(messages, prompt, signal, emit) {
      const token = await auth.getAccessToken();
      const endpoint = `https://aiplatform.googleapis.com/v1/projects/${config.projectId}/locations/global/publishers/google/models/${config.vertexModel}:streamGenerateContent?alt=sse`;
      const response = await fetchImpl(endpoint, { method: 'POST', signal,
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ systemInstruction: { parts: [{ text: prompt }] },
          contents: messages.map(m => ({ role: m.role === 'assistant' ? 'model' : 'user', parts: [{ text: m.content }] })),
          generationConfig: { maxOutputTokens: LIMITS.maxOutputTokens,
            thinkingConfig: { thinkingLevel: 'MINIMAL' },
            responseMimeType: 'application/json', responseSchema: RESPONSE_SCHEMA },
        }),
      });
      return consume(response, 'vertex', emit);
    },
    async openrouter(messages, prompt, signal, emit) {
      if (!config.openRouterKey) throw new GameError('backup_not_configured', 503);
      const response = await fetchImpl('https://openrouter.ai/api/v1/chat/completions', {
        method: 'POST', signal, headers: { Authorization: `Bearer ${config.openRouterKey}`, 'Content-Type': 'application/json',
          'X-Title': 'NYP Cloud Computing Game' },
        body: JSON.stringify({ model: config.openRouterModel, stream: true,
          max_tokens: LIMITS.maxOutputTokens, temperature: 0.7,
          reasoning: { enabled: false }, response_format: { type: 'json_object' },
          // Keep the backup on free providers. Never route silently onto a paid model.
          provider: { max_price: { prompt: 0, completion: 0 } },
          messages: [{ role: 'system', content: prompt }, ...messages] }),
      });
      return consume(response, 'openrouter', emit);
    },
  };
}
