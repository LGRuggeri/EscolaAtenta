import React, { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { turmasService } from '../../services/turmasService';
import { RootStackParamList } from '../../navigation/types';
import { SafeAreaView } from 'react-native-safe-area-context';
import { theme } from '../../theme/colors';
import { formStyles as styles } from '../../theme/formStyles';
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

