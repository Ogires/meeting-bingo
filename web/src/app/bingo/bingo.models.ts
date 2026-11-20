// The hub's messages, as the server's JSON protocol sends them (C# records in camelCase).

/** A phrase's room-wide state: how many players have it marked, and whether it has been confirmed. */
export interface PhraseUpdate {
  phraseId: number;
  marks: number;
  confirmed: boolean;
}

/** One cell of the player's card. The centre cell (index 12) is free: no phrase. */
export interface CellView {
  index: number;
  phraseId: number | null;
  text: string | null;
  marked: boolean;
}

/** What JoinRoom returns: everything needed to draw the card and the room. */
export interface PlayerSnapshot {
  token: string;
  alias: string;
  cells: CellView[];
  players: string[];
  phrases: PhraseUpdate[];
  lines: string[];
}

/** What ToggleCell returns. */
export interface ToggleResult {
  marked: boolean;
  phrase: PhraseUpdate;
  newLines: string[];
}

export interface BingoAnnouncement {
  alias: string;
  line: string;
}

/** The player's own part of the room: their alias, their card and the lines they have completed. */
export interface Me {
  alias: string;
  cells: CellView[];
  lines: string[];
}

export type ConnectionStatus =
  'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'closed';

export type BingoError =
  'room_not_found' | 'room_full' | 'room_closed' | 'rate_limited' | 'unknown';
