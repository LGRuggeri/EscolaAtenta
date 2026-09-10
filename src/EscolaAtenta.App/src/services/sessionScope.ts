/** Identidade sem colisões: codificação UTF-16 preserva servidor e usuário. */
export function databaseName(server: string, userId: string): string {
  if (!server || !userId) throw new Error('Servidor e usuário são obrigatórios.');
  const identity = JSON.stringify([server.replace(/\/+$/, ''), userId]);
  return 'ea2_' + identity.split('').map(c => c.charCodeAt(0).toString(16).padStart(4, '0')).join('');
}

export class SessionScope {
  generation = 0;
  active = false;
  private pending = new Set<Promise<unknown>>();
  assert(generation: number): void {
    if (generation !== this.generation) throw new Error('A sessão foi alterada.');
  }
  track<T>(operation: Promise<T>): Promise<T> {
    this.pending.add(operation);
    operation.then(() => this.pending.delete(operation), () => this.pending.delete(operation));
    return operation;
  }
  async invalidate(): Promise<void> {
    this.active = false;
    this.generation++;
    await Promise.allSettled([...this.pending]);
  }
}
export const sessionScope = new SessionScope();
