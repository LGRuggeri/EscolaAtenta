import { StyleSheet } from 'react-native';
import { theme } from './colors';

/**
 * Styles compartilhados entre telas de formulário (TurmaForm, AlunoForm, etc.).
 */
export const formStyles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    form: { padding: theme.spacing.lg },
    label: {
        fontSize: 14,
        fontWeight: '600',
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.sm,
    },
    saveButton: {
        backgroundColor: theme.colors.primary,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.md,
        alignItems: 'center',
        marginTop: theme.spacing.md,
    },
    saveButtonDisabled: { opacity: 0.7 },
    saveButtonText: {
        color: theme.colors.surface,
        fontSize: 16,
        fontWeight: 'bold',
    },
});
