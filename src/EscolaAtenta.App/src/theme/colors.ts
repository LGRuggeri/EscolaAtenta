/**
 * Design System Central do EscolaAtenta
 * Paleta Institucional (Sóbria e Moderna)
 *
 * PROIBIDO o uso de magic strings de cores diretamente nos componentes.
 * Todos os estilos devem importar objectos deste ficheiro.
 */

export const palette = {
    // Cores de Marca
    navy: '#283349',
    navyLight: '#3a4a66',
    charcoal: '#313f3f',

    // Verdes Institucionais
    olive: '#5f7d40',
    oliveLight: '#9ead8f',
    olivePale: '#d4e0cc',

    // Neutros
    white: '#FFFFFF',
    offWhite: '#f0f5ef',
    gray100: '#F3F4F6',
    gray300: '#c1c6ca',
    gray500: '#81878d',
    gray600: '#6B7280',
    gray700: '#4B5563',

    // Semânticas
    error: '#EF4444',
    errorLight: '#FEE2E2',
    warning: '#F59E0B',
    warningLight: '#FEF3C7',
    success: '#10B981',
    successLight: '#D1FAE5',
    info: '#3B82F6',
    infoLight: '#DBEAFE',
} as const;

export const theme = {
    colors: {
        // Cores de Marca e Ações Principais
        primary: palette.navy,
        primaryLight: palette.navyLight,
        primaryDark: palette.charcoal,

        // Cores Secundárias e Sucesso/Frequência
        secondary: palette.olive,
        secondaryLight: palette.oliveLight,
        secondaryPale: palette.olivePale,

        // Backgrounds e Superfícies
        background: palette.offWhite,
        surface: palette.white,
        surfaceVariant: palette.gray100,

        // Tipografia
        textPrimary: palette.navy,
        textSecondary: palette.gray500,
        textMuted: palette.gray600,

        // Útilitários
        border: palette.gray300,
        divider: palette.gray100,
        error: palette.error,
        errorLight: palette.errorLight,
        warning: palette.warning,
        warningLight: palette.warningLight,
        success: palette.success,
        successLight: palette.successLight,
        info: palette.info,
        infoLight: palette.infoLight,
    },

    spacing: {
        xs: 4,
        sm: 8,
        md: 16,
        lg: 24,
        xl: 32,
        xxl: 48,
    },

    borderRadius: {
        sm: 8,
        md: 12,
        lg: 16,
        xl: 24,
        full: 9999,
    },

    shadow: {
        sm: {
            elevation: 2,
            shadowColor: '#000',
            shadowOffset: { width: 0, height: 1 },
            shadowOpacity: 0.08,
            shadowRadius: 2,
        },
        md: {
            elevation: 4,
            shadowColor: '#000',
            shadowOffset: { width: 0, height: 2 },
            shadowOpacity: 0.12,
            shadowRadius: 4,
        },
        lg: {
            elevation: 8,
            shadowColor: '#000',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: 0.15,
            shadowRadius: 8,
        },
    },
};
