import React, { useState } from 'react';
import { ScrollView, View, Text, StyleSheet, TextInput, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { alunosService } from '../../services/alunosService';
import { RootStackParamList } from '../../navigation/types';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AxiosError } from 'axios';
import { HistoricoPresencasTimeline } from '../../components/domain/HistoricoPresencasTimeline';
import { theme } from '../../theme/colors';

type AlunoFormRouteProp = RouteProp<RootStackParamList, 'AlunoForm'>;

export function AlunoFormScreen() {
    const navigation = useNavigation();
    const route = useRoute<AlunoFormRouteProp>();

    // Captura os parâmetros corretamente
    const turmaId = route.params?.turmaId;
    const alunoParaEditar = route.params?.aluno;

    const [nome, setNome] = useState(alunoParaEditar?.nome || '');
    const [matricula, setMatricula] = useState(alunoParaEditar?.matricula || '');
    const [loading, setLoading] = useState(false);

    const isEditing = !!alunoParaEditar;

    async function handleSave() {
        if (!nome.trim()) {
            Alert.alert('Atenção', 'O nome do aluno é obrigatório.');
            return;
        }

        const payload = {
            nome: nome.trim(),
            matricula: matricula.trim(),
            turmaId: turmaId
        };

        try {
            setLoading(true);
            if (isEditing && alunoParaEditar.id) {
                await alunosService.atualizar(alunoParaEditar.id, {
                    id: alunoParaEditar.id,
                    nome: nome.trim(),
                    matricula: matricula.trim()
                });
                Alert.alert('Sucesso', 'Aluno atualizado com sucesso!');
            } else {
                await alunosService.criar(payload);
                Alert.alert('Sucesso', 'Aluno cadastrado com sucesso!');
            }
            navigation.goBack();
        } catch (err: unknown) {
            console.error(err);
            if (err && typeof err === 'object' && 'isAxiosError' in err) {
                const axiosError = err as AxiosError<{ message?: string, detail?: string }>;
                const problemDetail = axiosError.response?.data?.detail;
                Alert.alert('Erro', problemDetail || axiosError.response?.data?.message || 'Ocorreu um erro ao salvar o aluno.');
            } else {
                Alert.alert('Erro', 'Ocorreu um erro ao salvar o aluno.');
            }
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
                <Text style={styles.headerTitle}>{isEditing ? 'Editar Aluno' : 'Novo Aluno'}</Text>
            </View>

            <ScrollView contentContainerStyle={styles.form}>
                <Text style={styles.label}>Nome do Aluno *</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: João da Silva"
                    value={nome}
                    onChangeText={setNome}
                />

                <Text style={styles.label}>Matrícula (opcional)</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: 2024001"
                    value={matricula}
                    onChangeText={setMatricula}
                />

                <TouchableOpacity
                    style={[styles.saveButton, loading && styles.saveButtonDisabled]}
                    onPress={handleSave}
                    disabled={loading}
                >
                    {loading ? (
                        <ActivityIndicator color={theme.colors.surface} />
                    ) : (
                        <Text style={styles.saveButtonText}>Salvar Aluno</Text>
                    )}
                </TouchableOpacity>

                {isEditing && alunoParaEditar?.id && (
                    <HistoricoPresencasTimeline alunoId={alunoParaEditar.id} />
                )}
            </ScrollView>
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
    saveButtonText: { color: theme.colors.surface, fontSize: 16, fontWeight: 'bold' }
});
