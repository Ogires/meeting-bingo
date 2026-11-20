import { InjectionToken } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';

/** Just the part of SignalR's HubConnection that BingoConnection uses, so tests can replace it. */
export interface HubLike {
  start(): Promise<void>;
  invoke<T>(method: string, ...args: unknown[]): Promise<T>;
  // Same signature as HubConnection.on: each event has its own argument types
  on(event: string, handler: (...args: any[]) => void): void;
  onreconnecting(callback: () => void): void;
  onreconnected(callback: () => void): void;
  onclose(callback: (error?: Error) => void): void;
}

/** Creates a new hub connection. Same origin: the API serves the app, and the dev server proxies /hubs. */
export const HUB_FACTORY = new InjectionToken<() => HubLike>('HUB_FACTORY', {
  providedIn: 'root',
  factory: () => () =>
    new HubConnectionBuilder().withUrl('/hubs/bingo').withAutomaticReconnect().build(),
});
