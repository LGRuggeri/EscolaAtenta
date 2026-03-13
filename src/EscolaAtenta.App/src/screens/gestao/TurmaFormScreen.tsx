import React, { useState } from 'react';
import { View, Text, StyleSheet, TextInput, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { turmasService } from '../../services/turmasService';
import { RootStackParamList } from '../../navigation/types';
import { SafeAreaView } from 'react-native-safe-area-context';
import { theme } from '../../theme/colors';
import { syncWithServer } from '../../services/sync/watermelondbSync';

type TurmaFormRouteProp = RouteProp<RootStackParamList, 'TurmaForm'>;

export function TurmaFormScreen() {
    const navigation = useNavigation();
    const route = useRoute<TurmaFormRouteProp>();
    const turmaParaEditar = route.params?.turma;

    const [nome, setNome] = useState(turmaParaEditar?.nome || '');
    const [anoLetivo, setAnoLetivo] = useState(turmaParaEditar?.anoLetivo?.toString() || new Date().getFullYear().toString());
    const [turno, setTurno] = useState(turmaParaEditar?.turno || 'Matutino');
    const [loading, setLoading] = useState(false);

    const isEditing = !!turmaParaEditar;

    async function handleSave() {
        if (!nome.trim() || !anoLetivo.trim() || !turno.trim()) {
            Alert.alert('Atenção', 'Preencha todos os campos obrigatórios.');
            return;
        }

        const payload = {
            nome: nome.trim(),
            anoLetivo: parseInt(anoLetivo, 10),
            turno: turno.trim(),
        };

        try {
            setLoading(true);

            // Salva localmente — funciona sem Wi-Fi
            if (isEditing && turmaParaEditar.id) {
                await turmasService.atualizar(turmaParaEditar.id, payload);
            } else {
                await turmasService.criar(payload);
            }

            // Tenta sincronizar em background — falha silenciosamente sem rede
            syncWithServer().catch(() => {});

            navigation.goBack();
        } catch (err: unknown) {
            console.error(err);
            Alert.alert('Erro', 'Não foi possível salvar a turma localmente.');
        } finally {
            setLoading(false);
        }
    }

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>{isEditing ? 'Editar Turma' : 'Nova Turma'}</Text>
            </View>

            <View style={styles.form}>
                <Text style={styles.label}>Nome da Turma *</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: 5º Série A"
                    value={nome}
                    onChangeText={setNome}
                />

                <Text style={styles.label}>Ano Letivo *</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: 2026"
                    keyboardType="numeric"
                    value={anoLetivo}
                    onChangeText={setAnoLetivo}
                />

                <Text style={styles.label}>Turno *</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: Matutino, Vespertino, Noturno"
                    value={turno}
                    onChangeText={setTurno}
                />

                <TouchableOpacity
                    style={[styles.saveButton, loading && styles.saveButtonDisabled]}
                    onPress={handleSave}
                    disabled={loading}
                >
                    {loading ? (
                        <ActivityIndicator color={theme.colors.surface} />
                    ) : (
                        <Text style={styles.saveButtonText}>Salvar Turma</Text>
                    )}
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
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
