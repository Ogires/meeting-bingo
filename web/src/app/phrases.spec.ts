import { phrases } from './phrases';

describe('phrases', () => {
  it('has enough phrases for a card', () => {
    expect(phrases.length).toBeGreaterThanOrEqual(24);
  });

  it('has unique ids', () => {
    expect(new Set(phrases.map((p) => p.id)).size).toBe(phrases.length);
  });

  it('only uses the known categories', () => {
    const categories = new Set(phrases.map((p) => p.category));
    expect([...categories].every((c) => ['technical', 'jargon', 'situation'].includes(c))).toBe(true);
  });
});
