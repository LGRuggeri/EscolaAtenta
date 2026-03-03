export enum PapelUsuario {
  Monitor = 1,
  Supervisao = 2,
  Administrador = 3
}

export enum StatusPresenca {
  Presente = 0,
  Falta = 1,
  FaltaJustificada = 2,
  Ausente = 3,
  Atraso = 4
}

/**
 * Enum para níveis de alerta de evasão escolar.
 * 
 * IMPORTANTE: O backend serializa este enum como STRING (ex: "Preto"),
 * não como número (ex: 5). O frontend deve estar preparado para ambos.
 * 
 * Valores numéricos (para referência):
 * - 0: Excelencia
 * - 1: Aviso  
 * - 2: Intermediario
 * - 3: Vermelho (3-4 faltas)
 * - 5: Preto (5+ faltas) - MÁXIMO
 */
export enum NivelAlertaFalta {
  Excelencia = 0,
  Aviso = 1,
  Intermediario = 2,
  Vermelho = 3,
  // Valor 4 também mapeia para Vermelho (caso especial do backend)
  Preto = 5  // Conselho Tutelar (5+ atrasos ou falhas)
}

// Constantes para programação defensiva
export const NIVEL_MAXIMO_FALTAS = 5;
export const NIVEL_VERMELHO_THRESHOLD = 3;

/**
 * Converte uma string do enum para o valor numérico correspondente.
 * Útil quando o backend envia o enum como string (ex: "Preto" → 5).
 */
export function parseNivelAlertaFalta(valor: string | number): NivelAlertaFalta {
  if (typeof valor === 'number') {
    return valor as NivelAlertaFalta;
  }

  // Mapeamento de strings para valores
  const mapa: Record<string, NivelAlertaFalta> = {
    'Excelencia': NivelAlertaFalta.Excelencia,
    'Aviso': NivelAlertaFalta.Aviso,
    'Intermediario': NivelAlertaFalta.Intermediario,
    'Vermelho': NivelAlertaFalta.Vermelho,
    'Preto': NivelAlertaFalta.Preto
  };

  return mapa[valor] ?? NivelAlertaFalta.Preto; // Fallback para Preto se não reconhecido
}

/**
 * Discriminador de tipo de alerta enviado pelo backend.
 * Usa string enum para espelhar exatamente a serialização do C# (System.Text.Json).
 *
 * IMPORTANTE: Os valores devem ser idênticos ao que o C# serializa.
 * No backend: TipoAlerta.Evasao → "Evasao", TipoAlerta.Atraso → "Atraso"
 * (sem acentos — match exato do enum C#)
 */
export enum TipoAlerta {
  Evasao = 'Evasao',
  Atraso = 'Atraso',
}
