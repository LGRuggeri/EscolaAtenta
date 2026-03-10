import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { useRoute, useNavigation, RouteProp } from '@react-navigation/native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { Q } from '@nozbe/watermelondb';
import { RootStackParamList, AppNavigationProp } from '../../navigation/types';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import RegistroPresenca, { StatusPresencaLocal } from '../../database/models/RegistroPresenca';
import { theme } from '../../theme/colors';
import { syncWithServer } from '../../services/sync/watermelondbSync';

import withObservables from '@nozbe/with-observables';

type ChamadaRouteProp = RouteProp<RootStackParamList, 'ChamadaOperacao'>;

interface ChamadaScreenProps {
    route: ChamadaRouteProp;
    navigation: AppNavigationProp;
    alunos: Aluno[];
}

// Mapeamento de status para labels curtos e longos
const STATUS_OPTIONS: { key: StatusPresencaLocal; label: string; sub: string }[] = [
    { key: 'Presente', label: 'P', sub: 'Presente' },
    { key: 'Falta', label: 'F', sub: 'Falta' },
    { key: 'Atraso', label: 'A', sub: 'Atraso' },
    { key: 'FaltaJustificada', label: 'J', sub: 'Justif.' },
];

function ChamadaScreenRaw({ route, navigation, alunos }: ChamadaScreenProps) {
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [statusMap, setStatusMap] = useState<Record<string, StatusPresencaLocal>>({});

    // Inicializa o statusMap para todos os alunos encontrados como 'Presente' (se ainda não preenchido)
    useEffect(() => {
        if (alunos.length > 0 && Object.keys(statusMap).length === 0) {
            const initialMap: Record<string, StatusPresencaLocal> = {};
            alunos.forEach((a) => {
                initialMap[a.id] = 'Presente';
            });
            setStatusMap(initialMap);
        }
    }, [alunos]);

    const setStatus = (alunoId: string, status: StatusPresencaLocal) => {
        setStatusMap((prev) => ({ ...prev, [alunoId]: status }));
    };

    // ── Salva chamada no WatermelonDB local (instantâneo, zero rede) ─────
    const handleSalvar = async () => {
        if (alunos.length === 0) {
            Alert.alert('Aviso', 'Não há alunos nesta turma para registrar chamada.');
            return;
        }

        try {
            const registrosCollection = database.get<RegistroPresenca>('registros_presenca');

            await database.write(async () => {
                const batch = alunos.map((aluno) =>
                    registrosCollection.prepareCreate((record) => {
                        record.alunoId = aluno.id;
                        record.turmaId = turmaId;
                        record.data = new Date();
                        record.status = statusMap[aluno.id] ?? 'Presente';
                        record.sincronizado = false;
                    })
                );

                await database.batch(...batch);
            });

            navigation.goBack();

            // Dispara sync imediatamente após salvar (em background, sem bloquear a UI)
            syncWithServer().catch(() => {
                // Falha silenciosa — o polling de 60s vai retentar
            });
        } catch (error) {
            console.error('[CHAMADA] Erro ao salvar localmente:', error);
            Alert.alert('Erro', 'Falha ao salvar a chamada no dispositivo.');
        }
    };

    // ── Render ───────────────────────────────────────────────────────────
    const renderItem = ({ item }: { item: Aluno }) => {
        const currentStatus = statusMap[item.id] ?? 'Presente';

        return (
            <View style={styles.card}>
                <Text style={styles.alunoNome}>{item.nome}</Text>

                <View style={styles.statusRow}>
                    {STATUS_OPTIONS.map((opt) => (
                        <TouchableOpacity
                            key={opt.key}
                            style={[
                                styles.statusButton,
                                currentStatus === opt.key && getActiveStyle(opt.key),
                            ]}
                            onPress={() => setStatus(item.id, opt.key)}
                        >
                            <Text style={[styles.statusText, currentStatus === opt.key && styles.textWhite]}>
                                {opt.label}
                            </Text>
                            <Text style={[styles.statusSubText, currentStatus === opt.key && styles.textWhite]}>
                                {opt.sub}
                            </Text>
                        </TouchableOpacity>
                    ))}
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
                <FlatList
                    data={alunos}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                />
            </View>

            <View style={[styles.footer, { paddingBottom: Math.max(insets.bottom + 30, 40) }]}>
                <TouchableOpacity
                    style={styles.saveButton}
                    onPress={handleSalvar}
                >
                    <Text style={styles.saveButtonText}>Salvar Chamada</Text>
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
}

const EnhancedChamadaScreen = withObservables(['route'], ({ route }: { route: ChamadaRouteProp }) => ({
    alunos: database.get<Aluno>('alunos').query(Q.where('turma_id', route.params.turmaId))
}))(ChamadaScreenRaw);

export function ChamadaScreen() {
    const route = useRoute<ChamadaRouteProp>();
    const navigation = useNavigation<AppNavigationProp>();
    return <EnhancedChamadaScreen route={route} navigation={navigation} />;
}

// ── Helpers de estilo ────────────────────────────────────────────────────────

function getActiveStyle(status: StatusPresencaLocal) {
    switch (status) {
        case 'Presente': return styles.btnPresenteActive;
        case 'Falta': return styles.btnFaltaActive;
        case 'Atraso': return styles.btnAtrasoActive;
        case 'FaltaJustificada': return styles.btnJustificadaActive;
    }
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
    saveButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 18 },
});
