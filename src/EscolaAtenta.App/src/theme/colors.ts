/**
 * Design System Central do EscolaAtenta
 * Paleta Institucional (Sóbria e Moderna)
 *
 * PROIBIDO o uso de magic strings de cores diretamente nos componentes.
 * Todos os estilos devem importar objectos deste ficheiro.
 */

export const theme = {
    colors: {
        // Cores de Marca e Ações Principais
        primary: '#283349',       // Azul Marinho/Ardósia profundo (headers, botões submit, ícones ativos)
        primaryDark: '#313f3f',   // Charcoal escuro (pressed states)

        // Cores Secundárias e Sucesso/Frequência
        secondary: '#5f7d40',     // Verde Sóbrio/Oliva (botões sec., badges presente, sucesso)
        secondaryLight: '#9ead8f',// Verde mutado claro (fundos áreas de sucesso)

        // Backgrounds e Superfícies
        background: '#f0f5ef',    // Branco/Verde claro (fundo geral das telas)
        surface: '#FFFFFF',       // Branco puro (cards, modais, inputs)

        // Tipografia
        textPrimary: '#283349',   // Preto institucional (títulos, dados) - NUNCA usar #000000
        textSecondary: '#81878d', // Cinza médio (subtítulos, placeholders)

        // Útilitários
        border: '#c1c6ca',        // Cinza claro divisórias
        error: '#EF4444',         // Vermelho (alertas críticos, Faltas)
    }
};
