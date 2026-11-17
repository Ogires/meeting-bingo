import catalog from '../../../data/phrases.es.json';

export type PhraseCategory = 'technical' | 'jargon' | 'situation';

export interface Phrase {
  id: number;
  text: string;
  category: PhraseCategory;
}

/** The same file the server embeds: one source of truth for the printable card and the live game. */
export const phrases: readonly Phrase[] = catalog as Phrase[];
