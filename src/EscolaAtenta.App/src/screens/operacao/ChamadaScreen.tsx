import React, { useState, useEffect } from 'react';
import { View, StyleSheet, FlatList, Alert, Pressable } from 'react-native';
import { Text, Button, Surface } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { useRoute, useNavigation, RouteProp } from '@react-navigation/native';
import { Q } from '@nozbe/watermelondb';
import { RootStackParamList, AppNavigationProp } from '../../navigation/types';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import RegistroPresenca, { StatusPresencaLocal } from '../../database/models/RegistroPresenca';
import { AppHeader } from '../../components/ui';
import { theme, palette } from '../../theme/colors';
import { syncWithServer } from '../../services/sync/watermelondbSync';

import withObservables from '@nozbe/with-observables';

type ChamadaRouteProp = RouteProp<RootStackParamList, 'ChamadaOperacao'>;

interface ChamadaScreenProps {
    route: ChamadaRouteProp;
    navigation: AppNavigationProp;
    alunos: Aluno[];
}

const STATUS_OPTIONS: {
    key: StatusPresencaLocal;
    label: string;
    sub: string;
    icon: keyof typeof MaterialCommunityIcons.glyphMap;
    color: string;
    bgColor: string;
}[] = [
    { key: 'Presente', label: 'P', sub: 'Presente', icon: 'check-circle', color: theme.colors.success, bgColor: theme.colors.successLight },
    { key: 'Falta', label: 'F', sub: 'Falta', icon: 'close-circle', color: theme.colors.error, bgColor: theme.colors.errorLight },
    { key: 'Atraso', label: 'A', sub: 'Atraso', icon: 'clock-alert', color: theme.colors.warning, bgColor: theme.colors.warningLight },
    { key: 'FaltaJustificada', label: 'J', sub: 'Justif.', icon: 'file-document-check', color: theme.colors.info, bgColor: theme.colors.infoLight },
];

function ChamadaScreenRaw({ route, navigation, alunos }: ChamadaScreenProps) {
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [statusMap, setStatusMap] = useState<Record<string, StatusPresencaLocal>>({});

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
        Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
        setStatusMap((prev) => ({ ...prev, [alunoId]: status }));
    };

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

            Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
            navigation.goBack();

            syncWithServer().catch(() => {});
        } catch (error) {
            Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
            console.error('[CHAMADA] Erro ao salvar localmente:', error);
            Alert.alert('Erro', 'Falha ao salvar a chamada no dispositivo.');
        }
    };

    const renderItem = ({ item }: { item: Aluno }) => {
        const currentStatus = statusMap[item.id] ?? 'Presente';

        return (
            <Surface style={styles.card} elevation={1}>
                <Text variant="titleMedium" style={styles.alunoNome}>{item.nome}</Text>

                <View style={styles.statusRow}>
                    {STATUS_OPTIONS.map((opt) => {
                        const isActive = currentStatus === opt.key;
                        return (
                            <Pressable
                                key={opt.key}
                                style={[
                                    styles.statusButton,
                                    isActive && { backgroundColor: opt.color, borderColor: opt.color },
                                ]}
                                onPress={() => setStatus(item.id, opt.key)}
                            >
                                <MaterialCommunityIcons
                                    name={opt.icon}
                                    size={20}
                                    color={isActive ? palette.white : opt.color}
                                />
                                <Text
                                    variant="labelSmall"
                                    style={[
                                        styles.statusLabel,
                                        { color: isActive ? palette.white : opt.color },
                                    ]}
                                >
                                    {opt.sub}
                                </Text>
                            </Pressable>
                        );
                    })}
                </View>
            </Surface>
        );
    };

    // Contadores de resumo
    const resumo = Object.values(statusMap).reduce(
        (acc, s) => {
            acc[s] = (acc[s] ?? 0) + 1;
            return acc;
        },
        {} as Record<string, number>
    );

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Chamada Diária"
                subtitle={turmaNome}
                onBack={() => navigation.goBack()}
            />

            {/* Resumo visual */}
            {alunos.length > 0 && (
                <View style={styles.resumoBar}>
                    {STATUS_OPTIONS.map((opt) => (
                        <View key={opt.key} style={[styles.resumoItem, { backgroundColor: opt.bgColor }]}>
                            <MaterialCommunityIcons name={opt.icon} size={14} color={opt.color} />
                            <Text variant="labelSmall" style={{ color: opt.color, fontWeight: 'bold' }}>
                                {resumo[opt.key] ?? 0}
                            </Text>
                        </View>
                    ))}
                </View>
            )}

            <FlatList
                data={alunos}
                keyExtractor={(item) => item.id}
                renderItem={renderItem}
                contentContainerStyle={styles.listContainer}
            />

            <View style={[styles.footer, { paddingBottom: Math.max(insets.bottom + 16, 24) }]}>
                <Button
                    mode="contained"
                    onPress={handleSalvar}
                    icon="content-save-check"
                    style={styles.saveButton}
                    contentStyle={styles.saveButtonContent}
                    labelStyle={styles.saveButtonLabel}
                >
                    Salvar Chamada
                </Button>
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

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    resumoBar: {
        flexDirection: 'row',
        paddingHorizontal: theme.spacing.md,
        paddingVertical: theme.spacing.sm,
        gap: theme.spacing.sm,
        justifyContent: 'center',
    },
    resumoItem: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: theme.spacing.sm + 4,
        paddingVertical: theme.spacing.xs,
        borderRadius: theme.borderRadius.full,
    },
    listContainer: { padding: theme.spacing.md, paddingBottom: theme.spacing.lg },
    card: {
        backgroundColor: theme.colors.surface,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.md,
        marginBottom: theme.spacing.sm + 4,
    },
    alunoNome: {
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        marginBottom: theme.spacing.sm + 4,
    },
    statusRow: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
    },
    statusButton: {
        flex: 1,
        borderWidth: 1.5,
        borderColor: theme.colors.border,
        borderRadius: theme.borderRadius.sm,
        paddingVertical: theme.spacing.sm + 2,
        alignItems: 'center',
        justifyContent: 'center',
        gap: 2,
    },
    statusLabel: {
        fontWeight: '700',
        fontSize: 11,
    },
    footer: {
        padding: theme.spacing.md,
        paddingTop: theme.spacing.sm + 4,
        backgroundColor: theme.colors.surface,
        borderTopWidth: 1,
        borderColor: theme.colors.divider,
        ...theme.shadow.sm,
    },
    saveButton: {
        borderRadius: theme.borderRadius.md,
    },
    saveButtonContent: {
        paddingVertical: theme.spacing.sm,
    },
    saveButtonLabel: {
        fontSize: 16,
        fontWeight: 'bold',
    },
});
