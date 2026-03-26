import React, { useState } from 'react';
import { View, StyleSheet, Alert, ScrollView } from 'react-native';
import { TextInput, Button } from 'react-native-paper';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { turmasService } from '../../services/turmasService';
import { RootStackParamList } from '../../navigation/types';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppHeader } from '../../components/ui';
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
            if (isEditing && turmaParaEditar.id) {
                await turmasService.atualizar(turmaParaEditar.id, payload);
            } else {
                await turmasService.criar(payload);
            }
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
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title={isEditing ? 'Editar Turma' : 'Nova Turma'}
                onBack={() => navigation.goBack()}
            />

            <ScrollView contentContainerStyle={styles.form}>
                <TextInput
                    label="Nome da Turma *"
                    placeholder="Ex: 5º Série A"
                    value={nome}
                    onChangeText={setNome}
                    mode="outlined"
                    left={<TextInput.Icon icon="google-classroom" />}
                    style={styles.input}
                />

                <TextInput
                    label="Ano Letivo *"
                    placeholder="Ex: 2026"
                    keyboardType="numeric"
                    value={anoLetivo}
                    onChangeText={setAnoLetivo}
                    mode="outlined"
                    left={<TextInput.Icon icon="calendar" />}
                    style={styles.input}
                />

                <TextInput
                    label="Turno *"
                    placeholder="Ex: Matutino, Vespertino, Noturno"
                    value={turno}
                    onChangeText={setTurno}
                    mode="outlined"
                    left={<TextInput.Icon icon="weather-sunset-up" />}
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
                    Salvar Turma
                </Button>
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    form: { padding: theme.spacing.lg },
    input: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    saveButton: { marginTop: theme.spacing.sm, borderRadius: theme.borderRadius.md },
    saveButtonContent: { paddingVertical: theme.spacing.xs },
});
