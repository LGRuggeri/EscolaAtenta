import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { useRoute, useNavigation, RouteProp } from '@react-navigation/native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { RootStackParamList, AppNavigationProp } from '../../navigation/types';
import { chamadasService } from '../../services/chamadasService';
import { alunosService } from '../../services/alunosService';
import { useAuth } from '../../hooks/useAuth';
import { AlunoDto, RealizarChamadaPayload } from '../../types/dtos';
import * as Enums from '../../types/enums';
import { theme } from '../../theme/colors';

type ChamadaRouteProp = RouteProp<RootStackParamList, 'ChamadaOperacao'>;

export function ChamadaScreen() {
    const route = useRoute<ChamadaRouteProp>();
    const navigation = useNavigation<AppNavigationProp>();
    const { user } = useAuth();
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [alunos, setAlunos] = useState<AlunoDto[]>([]);
    const [statusMap, setStatusMap] = useState<Record<string, Enums.StatusPresenca>>({});
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        carregarAlunos();
    }, [turmaId]);

    async function carregarAlunos() {
        try {
            setLoading(true);
            const data = await alunosService.obterPorTurma(turmaId);
            setAlunos(data);

            const initialMap: Record<string, Enums.StatusPresenca> = {};
            data.forEach(a => {
                initialMap[a.id] = Enums.StatusPresenca.Presente; // Default Presente
            });
            setStatusMap(initialMap);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar os alunos.');
        } finally {
            setLoading(false);
        }
    }

    const setStatus = (alunoId: string, status: Enums.StatusPresenca) => {
        setStatusMap(prev => ({ ...prev, [alunoId]: status }));
    };

    const handleSalvar = async () => {
        if (alunos.length === 0) {
            Alert.alert('Aviso', 'Não há alunos nesta turma para registrar chamada.');
            return;
        }

        try {
            setSaving(true);
            const payload: RealizarChamadaPayload = {
                turmaId,
                responsavelId: user?.id || '',
                alunos: alunos.map(a => ({
                    alunoId: a.id,
                    status: statusMap[a.id]
                }))
            };

            await chamadasService.realizarChamada(payload);
            Alert.alert('Sucesso', 'Chamada salva com sucesso!');
            navigation.goBack();
        } catch (error) {
            Alert.alert('Erro', 'Houve um problema ao salvar a chamada. Tente novamente.');
            console.error(error);
        } finally {
            setSaving(false);
        }
    };

    const renderItem = ({ item }: { item: AlunoDto }) => {
        const currentStatus = statusMap[item.id];

        return (
            <View style={styles.card}>
                <Text style={styles.alunoNome}>{item.nome}</Text>

                <View style={styles.statusRow}>
                    <TouchableOpacity
                        style={[styles.statusButton, currentStatus === Enums.StatusPresenca.Presente && styles.btnPresenteActive]}
                        onPress={() => setStatus(item.id, Enums.StatusPresenca.Presente)}
                    >
                        <Text style={[styles.statusText, currentStatus === Enums.StatusPresenca.Presente && styles.textWhite]}>P</Text>
                        <Text style={[styles.statusSubText, currentStatus === Enums.StatusPresenca.Presente && styles.textWhite]}>Presente</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.statusButton, currentStatus === Enums.StatusPresenca.Falta && styles.btnFaltaActive]}
                        onPress={() => setStatus(item.id, Enums.StatusPresenca.Falta)}
                    >
                        <Text style={[styles.statusText, currentStatus === Enums.StatusPresenca.Falta && styles.textWhite]}>F</Text>
                        <Text style={[styles.statusSubText, currentStatus === Enums.StatusPresenca.Falta && styles.textWhite]}>Falta</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.statusButton, currentStatus === Enums.StatusPresenca.Atraso && styles.btnAtrasoActive]}
                        onPress={() => setStatus(item.id, Enums.StatusPresenca.Atraso)}
                    >
                        <Text style={[styles.statusText, currentStatus === Enums.StatusPresenca.Atraso && styles.textWhite]}>A</Text>
                        <Text style={[styles.statusSubText, currentStatus === Enums.StatusPresenca.Atraso && styles.textWhite]}>Atraso</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.statusButton, currentStatus === Enums.StatusPresenca.FaltaJustificada && styles.btnJustificadaActive]}
                        onPress={() => setStatus(item.id, Enums.StatusPresenca.FaltaJustificada)}
                    >
                        <Text style={[styles.statusText, currentStatus === Enums.StatusPresenca.FaltaJustificada && styles.textWhite]}>J</Text>
                        <Text style={[styles.statusSubText, currentStatus === Enums.StatusPresenca.FaltaJustificada && styles.textWhite]}>Justif.</Text>
                    </TouchableOpacity>
                </View>
            </View>
        );
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <View style={styles.headerTitleContainer}>
                    <Text style={styles.headerTitle}>Chamada Diária</Text>
                    <Text style={styles.headerSubtitle}>{turmaNome}</Text>
                </View>
            </View>

            <View style={{ flex: 1 }}>
                {loading ? (
                    <ActivityIndicator size="large" color={theme.colors.secondary} style={{ marginTop: 50 }} />
                ) : (
                    <FlatList
                        data={alunos}
                        keyExtractor={(item) => item.id}
                        renderItem={renderItem}
                        contentContainerStyle={styles.listContainer}
                    />
                )}
            </View>

            <View style={[styles.footer, { paddingBottom: Math.max(insets.bottom + 30, 40) }]}>
                <TouchableOpacity
                    style={[styles.saveButton, (saving || loading) && styles.saveButtonDisabled]}
                    onPress={handleSalvar}
                    disabled={saving || loading}
                >
                    {saving ? (
                        <ActivityIndicator color="#FFF" />
                    ) : (
                        <Text style={styles.saveButtonText}>Salvar Chamada</Text>
                    )}
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    header: { flexDirection: 'row', alignItems: 'center', padding: 20, backgroundColor: theme.colors.surface, elevation: 2 },
    backButton: { position: 'absolute', left: 20, zIndex: 10 },
    backButtonText: { fontSize: 16, color: theme.colors.primary, fontWeight: '600' },
    headerTitleContainer: { flex: 1, alignItems: 'center' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: theme.colors.textPrimary },
    headerSubtitle: { fontSize: 14, color: theme.colors.textSecondary },
    listContainer: { padding: 16, paddingBottom: 24 },
    card: { backgroundColor: theme.colors.surface, padding: 16, borderRadius: 12, marginBottom: 12, elevation: 1 },
    alunoNome: { fontSize: 16, fontWeight: 'bold', color: theme.colors.textPrimary, marginBottom: 12 },
    statusRow: { flexDirection: 'row', justifyContent: 'space-between' },
    statusButton: { flex: 1, borderWidth: 1, borderColor: theme.colors.border, borderRadius: 8, paddingVertical: 8, marginHorizontal: 2, alignItems: 'center', justifyContent: 'center' },
    statusText: { fontSize: 16, fontWeight: 'bold', color: theme.colors.textSecondary },
    statusSubText: { fontSize: 10, color: theme.colors.textSecondary, marginTop: 2 },
    textWhite: { color: theme.colors.surface },
    btnPresenteActive: { backgroundColor: theme.colors.secondary, borderColor: theme.colors.secondary },
    btnFaltaActive: { backgroundColor: theme.colors.error, borderColor: theme.colors.error },
    btnAtrasoActive: { backgroundColor: theme.colors.primaryDark, borderColor: theme.colors.primaryDark },
    btnJustificadaActive: { backgroundColor: theme.colors.primary, borderColor: theme.colors.primary },
    footer: { padding: 20, paddingTop: 16, backgroundColor: theme.colors.surface, borderTopWidth: 1, borderColor: theme.colors.border },
    saveButton: { backgroundColor: theme.colors.primary, paddingVertical: 16, borderRadius: 12, alignItems: 'center' },
    saveButtonDisabled: { opacity: 0.7, backgroundColor: theme.colors.border },
    saveButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 18 }
});
