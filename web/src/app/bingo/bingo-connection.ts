import { Injectable, inject, signal } from '@angular/core';
import {
  BingoAnnouncement,
  BingoError,
  ConnectionStatus,
  Me,
  PhraseUpdate,
  PlayerSnapshot,
  ToggleResult,
} from './bingo.models';
import { HUB_FACTORY, HubLike } from './hub';

const KNOWN_ERRORS: readonly BingoError[] = [
  'room_not_found',
  'room_full',
  'room_closed',
  'rate_limited',
];

/**
 * The live room for this browser tab: one SignalR connection, and the room's state as signals.
 * Every change comes from the server (the result of a call or an event), so the state never guesses.
 */
@Injectable({ providedIn: 'root' })
export class BingoConnection {
  private readonly createHub = inject(HUB_FACTORY);
  private hub?: HubLike;
  private code?: string;

  private readonly _status = signal<ConnectionStatus>('idle');
  private readonly _me = signal<Me | null>(null);
  private readonly _players = signal<readonly string[]>([]);
  private readonly _phrases = signal<ReadonlyMap<number, PhraseUpdate>>(new Map());
  private readonly _announcements = signal<readonly BingoAnnouncement[]>([]);
  private readonly _error = signal<BingoError | null>(null);

  readonly status = this._status.asReadonly();
  readonly me = this._me.asReadonly();
  readonly players = this._players.asReadonly();
  /** phraseId → how many players have it marked, and whether it is confirmed. */
  readonly phrases = this._phrases.asReadonly();
  readonly announcements = this._announcements.asReadonly();
  readonly error = this._error.asReadonly();

  async join(code: string): Promise<void> {
    this.code = code.trim().toUpperCase();
    this._error.set(null);
    this._announcements.set([]);
    this._status.set('connecting');

    const hub = this.createHub();
    this.hub = hub;
    this.listen(hub);

    try {
      await hub.start();
      await this.enterRoom(hub, this.code);
      this._status.set('connected');
    } catch (error) {
      this.fail(error);
    }
  }

  async toggle(index: number): Promise<void> {
    if (!this.hub || this._status() !== 'connected') return;
    try {
      const result = await this.hub.invoke<ToggleResult>('ToggleCell', index);
      this._me.update(
        (me) =>
          me && {
            ...me,
            cells: me.cells.map((cell) =>
              cell.index === index ? { ...cell, marked: result.marked } : cell,
            ),
            lines: [...me.lines, ...result.newLines],
          },
      );
      this.setPhrase(result.phrase);
    } catch (error) {
      this._error.set(errorCode(error));
    }
  }

  private listen(hub: HubLike): void {
    hub.on('PlayerJoined', (alias: string) =>
      this._players.update((players) => (players.includes(alias) ? players : [...players, alias])),
    );
    hub.on('PlayerLeft', (alias: string) =>
      this._players.update((players) => players.filter((player) => player !== alias)),
    );
    hub.on('PhraseMarked', (update: PhraseUpdate) => this.setPhrase(update));
    hub.on('Bingo', (announcement: BingoAnnouncement) =>
      this._announcements.update((announcements) => [...announcements, announcement]),
    );
    hub.on('RoomClosed', () => {
      this._status.set('closed');
      this._error.set('room_closed');
    });

    hub.onreconnecting(() => this._status.set('reconnecting'));
    // The new connection is not in the room's group yet: join again with the token to get back in
    hub.onreconnected(async () => {
      if (!this.code) return;
      try {
        await this.enterRoom(hub, this.code);
        this._status.set('connected');
      } catch (error) {
        this.fail(error);
      }
    });
    hub.onclose(() => {
      if (this._status() !== 'closed') this._status.set('disconnected');
    });
  }

  private async enterRoom(hub: HubLike, code: string): Promise<void> {
    const snapshot = await hub.invoke<PlayerSnapshot>('JoinRoom', code, readToken(code));
    saveToken(code, snapshot.token);
    this._me.set({ alias: snapshot.alias, cells: snapshot.cells, lines: snapshot.lines });
    this._players.set(snapshot.players);
    this._phrases.set(new Map(snapshot.phrases.map((phrase) => [phrase.phraseId, phrase])));
  }

  private setPhrase(update: PhraseUpdate): void {
    this._phrases.update((phrases) => new Map(phrases).set(update.phraseId, update));
  }

  private fail(error: unknown): void {
    this._error.set(errorCode(error));
    this._status.set('disconnected');
  }
}

/** SignalR wraps a HubException: "… on the server. HubException: room_not_found". */
function errorCode(error: unknown): BingoError {
  const code = error instanceof Error ? /HubException: (\w+)/.exec(error.message)?.[1] : undefined;
  return KNOWN_ERRORS.find((known) => known === code) ?? 'unknown';
}

// The token lets a player get their card back after a reload. sessionStorage can throw
// (private mode, blocked storage): then the player simply joins as someone new.
const tokenKey = (code: string) => `meeting-bingo:token:${code}`;

function readToken(code: string): string | null {
  try {
    return sessionStorage.getItem(tokenKey(code));
  } catch {
    return null;
  }
}

function saveToken(code: string, token: string): void {
  try {
    sessionStorage.setItem(tokenKey(code), token);
  } catch {
    // Not saved: a reload will join as a new player
  }
}
