// The shapes of data crossing the wire to/from the frontend.

// One prior turn of the conversation (role is "user" or "assistant").
public record ChatTurn(string Role, string Content);

// A question plus the conversation so far, so follow-ups like "another one" resolve.
// (/ask streams its answer + citations as SSE, so there is no response DTO.)
public record AskRequest(string Question, ChatTurn[]? History = null);

// Bot-friendly answer for /ask/bot: the whole answer in one JSON object instead
// of SSE, so external automations (n8n / WhatsApp) can consume it in one call.
public record BotSource(string Text, float Score);
public record BotAskResponse(string Answer, BotSource[] Sources);
