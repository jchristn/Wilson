export function parseSseFrame(frame) {
  let event = '';
  const data = [];
  frame.split(/\r?\n/).forEach(line => {
    if (line.startsWith('event:')) event = line.slice(6).trim();
    if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
  });
  return { event, data: data.join('\n') };
}

export function mergeToolProgress(calls, progress) {
  if (!progress || !progress.toolCallId) return calls;
  const nextCall = {
    toolCallId: progress.toolCallId,
    runId: progress.runId || '',
    toolName: progress.toolName || '',
    displayLabel: progress.displayLabel || progress.toolName || 'Tool',
    iteration: progress.iteration || 0,
    sequenceNumber: progress.sequenceNumber || 0,
    success: progress.success,
    denied: progress.denied,
    truncated: progress.truncated,
    outputCharacters: progress.outputCharacters || 0,
    resultCount: progress.resultCount,
    elapsedMs: progress.elapsedMs || 0,
    summary: progress.summary || '',
    startedUtc: progress.startedUtc || '',
    completedUtc: progress.completedUtc || '',
    statusCode: progress.statusCode || '',
    approvalEndpoint: progress.approvalEndpoint || '',
    approvalExpiresUtc: progress.approvalExpiresUtc || ''
  };
  const index = calls.findIndex(call => call.toolCallId === nextCall.toolCallId);
  if (index < 0) return [...calls, nextCall];
  return calls.map((call, callIndex) => callIndex === index ? { ...call, ...nextCall } : call);
}
