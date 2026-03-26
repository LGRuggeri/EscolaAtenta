import { MD3LightTheme } from 'react-native-paper';
import { palette } from './colors';

export const paperTheme = {
    ...MD3LightTheme,
    roundness: 12,
    colors: {
        ...MD3LightTheme.colors,
        primary: palette.navy,
        onPrimary: palette.white,
        primaryContainer: palette.navyLight,
        onPrimaryContainer: palette.white,

        secondary: palette.olive,
        onSecondary: palette.white,
        secondaryContainer: palette.olivePale,
        onSecondaryContainer: palette.charcoal,

        tertiary: palette.info,
        onTertiary: palette.white,

        background: palette.offWhite,
        onBackground: palette.navy,

        surface: palette.white,
        onSurface: palette.navy,
        surfaceVariant: palette.gray100,
        onSurfaceVariant: palette.gray600,

        outline: palette.gray300,
        outlineVariant: palette.gray100,

        error: palette.error,
        onError: palette.white,
        errorContainer: palette.errorLight,
        onErrorContainer: palette.error,

        elevation: {
            ...MD3LightTheme.colors.elevation,
            level0: 'transparent',
            level1: palette.white,
            level2: palette.offWhite,
            level3: palette.gray100,
        },
    },
};

export type AppTheme = typeof paperTheme;
