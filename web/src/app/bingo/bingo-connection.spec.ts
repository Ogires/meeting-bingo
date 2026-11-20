import { TestBed } from '@angular/core/testing';
import { BingoConnection } from './bingo-connection';
import { CellView, PlayerSnapshot, ToggleResult } from './bingo.models';
import { HUB_FACTORY, HubLike } from './hub';

type Responder = (...args: unknown[]) => unknown;

/** A hub that records the calls, answers them with canned responses and can raise server events. */
class FakeHub implements HubLike {
  readonly calls: { method: string; args: unknown[] }[] = [];
  private readonly handlers = new Map<string, (...args: unknown[]) => void>();
  private readonly responders = new Map<string, Responder>();
  private reconnected?: () => void;
  private reconnecting?: () => void;

  respond(method: string, responder: Responder): void {
    this.responders.set(method, responder);
  }

  start(): Promise<void> {
    return Promise.resolve();
  }

  async invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    this.calls.push({ method, args });
    const responder = this.responders.get(method);
    if (!responder) throw new Error(`No response for ${method}`);
    return responder(...args) as T;
  }

  on(event: string, handler: (...args: unknown[]) => void): void {
    this.handlers.set(event, handler);
  }

  onreconnecting(callback: () => void): void {
    this.reconnecting = callback;
  }

  onreconnected(callback: () => void): void {
    this.reconnected = callback;
  }

  onclose(): void {}

  emit(event: string, ...args: unknown[]): void {
    this.handlers.get(event)?.(...args);
  }

  simulateReconnecting(): void {
    this.reconnecting?.();
  }

  async simulateReconnected(): Promise<void> {
    await (this.reconnected?.() as unknown);
  }
}

/** What the server sends on the wire for a HubException: the code comes after "HubException: ". */
const hubError = (code: string) =>
  new Error(
    `An unexpected error occurred invoking 'JoinRoom' on the server. HubException: ${code}`,
  );

const cells = (): CellView[] =>
  Array.from({ length: 25 }, (_, index) =>
    index === 12
      ? { index, phraseId: null, text: null, marked: true }
      : { index, phraseId: index + 1, text: `Frase ${index + 1}`, marked: false },
  );

const snapshot = (overrides: Partial<PlayerSnapshot> = {}): PlayerSnapshot => ({
  token: 'token-1',
  alias: 'Gato Ninja',
  cells: cells(),
  players: ['Gato Ninja', 'Pato Zen'],
  phrases: [{ phraseId: 3, marks: 1, confirmed: false }],
  lines: [],
  ...overrides,
});

describe('BingoConnection', () => {
  let hub: FakeHub;
  let connection: BingoConnection;

  beforeEach(() => {
    sessionStorage.clear();
    hub = new FakeHub();
    hub.respond('JoinRoom', () => snapshot());
    TestBed.configureTestingModule({ providers: [{ provide: HUB_FACTORY, useValue: () => hub }] });
    connection = TestBed.inject(BingoConnection);
  });

  afterEach(() => vi.restoreAllMocks());

  it('joins the room, keeps the token and fills the state', async () => {
    await connection.join('ABCDE');

    expect(hub.calls[0]).toEqual({ method: 'JoinRoom', args: ['ABCDE', null] });
    expect(connection.status()).toBe('connected');
    expect(connection.me()?.alias).toBe('Gato Ninja');
    expect(connection.me()?.cells).toHaveLength(25);
    expect(connection.players()).toEqual(['Gato Ninja', 'Pato Zen']);
    expect(connection.phrases().get(3)).toEqual({ phraseId: 3, marks: 1, confirmed: false });
    expect(sessionStorage.getItem('meeting-bingo:token:ABCDE')).toBe('token-1');
  });

  it('sends the saved token when joining the same room again', async () => {
    await connection.join('ABCDE');
    await connection.join(' abcde ');

    expect(hub.calls.at(-1)).toEqual({ method: 'JoinRoom', args: ['ABCDE', 'token-1'] });
  });

  it('still joins, as a new player, when the browser blocks storage', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });

    await connection.join('ABCDE');

    expect(hub.calls[0].args).toEqual(['ABCDE', null]);
    expect(connection.status()).toBe('connected');
  });

  it('keeps the player list as a set', async () => {
    await connection.join('ABCDE');

    hub.emit('PlayerJoined', 'Lince Zen');
    hub.emit('PlayerJoined', 'Lince Zen');
    hub.emit('PlayerLeft', 'Pato Zen');

    expect(connection.players()).toEqual(['Gato Ninja', 'Lince Zen']);
  });

  it('updates a phrase when someone marks it', async () => {
    await connection.join('ABCDE');

    hub.emit('PhraseMarked', { phraseId: 3, marks: 2, confirmed: true });
    hub.emit('PhraseMarked', { phraseId: 7, marks: 1, confirmed: false });

    expect(connection.phrases().get(3)).toEqual({ phraseId: 3, marks: 2, confirmed: true });
    expect(connection.phrases().get(7)?.marks).toBe(1);
  });

  it('collects the lines announced to the room', async () => {
    await connection.join('ABCDE');

    hub.emit('Bingo', { alias: 'Pato Zen', line: 'row-1' });

    expect(connection.announcements()).toEqual([{ alias: 'Pato Zen', line: 'row-1' }]);
  });

  it('joins the room again with its token after reconnecting', async () => {
    await connection.join('ABCDE');

    hub.simulateReconnecting();
    expect(connection.status()).toBe('reconnecting');

    hub.respond('JoinRoom', () => snapshot({ players: ['Gato Ninja'] }));
    await hub.simulateReconnected();

    expect(hub.calls.at(-1)).toEqual({ method: 'JoinRoom', args: ['ABCDE', 'token-1'] });
    expect(connection.status()).toBe('connected');
    expect(connection.players()).toEqual(['Gato Ninja']);
  });

  it('reports the code of a server error', async () => {
    hub.respond('JoinRoom', () => {
      throw hubError('room_not_found');
    });

    await connection.join('ZZZZZ');

    expect(connection.error()).toBe('room_not_found');
    expect(connection.status()).toBe('disconnected');
  });

  it('reports an unknown error when the message has no code', async () => {
    hub.respond('JoinRoom', () => {
      throw new Error('Failed to complete negotiation with the server');
    });

    await connection.join('ABCDE');

    expect(connection.error()).toBe('unknown');
  });

  it('marks the room as closed when the server closes it', async () => {
    await connection.join('ABCDE');

    hub.emit('RoomClosed');

    expect(connection.status()).toBe('closed');
    expect(connection.error()).toBe('room_closed');
  });

  it('updates the cell, the phrase and the lines with what the server returns', async () => {
    await connection.join('ABCDE');
    const result: ToggleResult = {
      marked: true,
      phrase: { phraseId: 1, marks: 1, confirmed: false },
      newLines: ['row-1'],
    };
    hub.respond('ToggleCell', () => result);

    await connection.toggle(0);

    expect(hub.calls.at(-1)).toEqual({ method: 'ToggleCell', args: [0] });
    expect(connection.me()?.cells[0].marked).toBe(true);
    expect(connection.me()?.cells[1].marked).toBe(false);
    expect(connection.phrases().get(1)?.marks).toBe(1);
    expect(connection.me()?.lines).toEqual(['row-1']);
  });

  it('keeps what the server says, not its own guess', async () => {
    await connection.join('ABCDE');
    // e.g. the same player unmarked it in another tab first: the server answers "not marked"
    hub.respond('ToggleCell', () => ({
      marked: false,
      phrase: { phraseId: 1, marks: 0, confirmed: false },
      newLines: [],
    }));

    await connection.toggle(0);

    expect(connection.me()?.cells[0].marked).toBe(false);
  });
});
