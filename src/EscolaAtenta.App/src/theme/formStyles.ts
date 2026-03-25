import { StyleSheet } from 'react-native';
import { theme } from './colors';

/**
 * Styles compartilhados entre telas de formulário (TurmaForm, AlunoForm, etc.).
 */
export const formStyles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    header: { flexDirection: 'row', alignItems: 'center', padding: 20, paddingTop: 20, backgroundColor: theme.colors.surface, elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    backButton: { marginRight: 16 },
    backButtonText: { fontSize: 16, color: theme.colors.primary, fontWeight: '600' },
    headerTitle: { fontSize: 20, fontWeight: 'bold', color: theme.colors.textPrimary },
    form: { padding: 20 },
    label: { fontSize: 14, fontWeight: '600', color: theme.colors.textSecondary, marginBottom: 8 },
    input: { backgroundColor: theme.colors.surface, borderWidth: 1, borderColor: theme.colors.border, borderRadius: 8, padding: 12, fontSize: 16, marginBottom: 20, color: theme.colors.textPrimary },
    saveButton: { backgroundColor: theme.colors.primary, padding: 16, borderRadius: 12, alignItems: 'center', marginTop: 12 },
    saveButtonDisabled: { opacity: 0.7 },
    saveButtonText: { color: theme.colors.surface, fontSize: 16, fontWeight: 'bold' },
});
