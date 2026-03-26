import React, { useState } from 'react';
import { StyleSheet, Alert, ScrollView } from 'react-native';
import { TextInput, Button } from 'react-native-paper';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { alunosService } from '../../services/alunosService';
import { RootStackParamList } from '../../navigation/types';
import { SafeAreaView } from 'react-native-safe-area-context';
import { HistoricoPresencasTimeline } from '../../components/domain/HistoricoPresencasTimeline';
import { AppHeader } from '../../components/ui';
import { theme } from '../../theme/colors';
import { syncWithServer } from '../../services/sync/watermelondbSync';

type AlunoFormRouteProp = RouteProp<RootStackParamList, 'AlunoForm'>;

export function AlunoFormScreen() {
    const navigation = useNavigation();
    const route = useRoute<AlunoFormRouteProp>();

    const turmaId = route.params?.turmaId;
    const alunoParaEditar = route.params?.aluno;

    const [nome, setNome] = useState(alunoParaEditar?.nome || '');
    const [loading, setLoading] = useState(false);

    const isEditing = !!alunoParaEditar;

    async function handleSave() {
        if (!nome.trim()) {
            Alert.alert('Atenção', 'O nome do aluno é obrigatório.');
            return;
        }

        try {
            setLoading(true);
            if (isEditing && alunoParaEditar.id) {
                await alunosService.atualizar(alunoParaEditar.id, {
                    id: alunoParaEditar.id,
                    nome: nome.trim(),
                });
            } else {
                await alunosService.criar({ nome: nome.trim(), turmaId });
            }
            syncWithServer().catch(() => {});
            navigation.goBack();
        } catch (err) {
            console.error('[AlunoForm]', err);
            Alert.alert('Erro', 'Não foi possível salvar o aluno localmente.');
        } finally {
            setLoading(false);
        }
    }

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title={isEditing ? 'Editar Aluno' : 'Novo Aluno'}
                onBack={() => navigation.goBack()}
            />

            <ScrollView contentContainerStyle={styles.form}>
                <TextInput
                    label="Nome do Aluno *"
                    placeholder="Ex: João da Silva"
                    value={nome}
                    onChangeText={setNome}
                    mode="outlined"
                    left={<TextInput.Icon icon="account" />}
                    style={styles.input}
                />

                <Button
                    mode="contained"
                    onPress={handleSave}
                    loading={loading}
                    disabled={loading}
                    icon="content-save"
                    style={styles.saveButton}
                    contentStyle={styles.saveButtonContent}
                >
                    Salvar Aluno
                </Button>

                {isEditing && alunoParaEditar?.id && (
                    <HistoricoPresencasTimeline alunoId={alunoParaEditar.id} />
                )}
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    form: { padding: theme.spacing.lg },
    input: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    saveButton: { marginTop: theme.spacing.sm, borderRadius: theme.borderRadius.md, marginBottom: theme.spacing.lg },
    saveButtonContent: { paddingVertical: theme.spacing.xs },
});
