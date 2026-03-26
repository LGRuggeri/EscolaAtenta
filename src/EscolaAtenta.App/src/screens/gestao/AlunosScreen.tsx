import React, { useState, useEffect } from 'react';
import { View, StyleSheet, FlatList } from 'react-native';
import { Text, FAB, ActivityIndicator, Card, Surface } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { AppNavigationProp, RootStackParamList } from '../../navigation/types';
import { AlunoDto } from '../../types/dtos';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import { Q } from '@nozbe/watermelondb';

type AlunosRouteProp = RouteProp<RootStackParamList, 'Alunos'>;

function StatBadge({ icon, label, value, color }: { icon: keyof typeof MaterialCommunityIcons.glyphMap; label: string; value: number; color: string }) {
    return (
        <View style={[styles.statBadge, { backgroundColor: color + '15' }]}>
            <MaterialCommunityIcons name={icon} size={14} color={color} />
            <Text variant="labelSmall" style={{ color, fontWeight: '600' }}>{value}</Text>
            <Text variant="labelSmall" style={{ color: theme.colors.textMuted, fontSize: 10 }}>{label}</Text>
        </View>
    );
}

export function AlunosScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const route = useRoute<AlunosRouteProp>();
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [alunos, setAlunos] = useState<AlunoDto[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const subscription = database
            .get<Aluno>('alunos')
            .query(Q.where('turma_id', turmaId))
            .observeWithColumns([
                'faltas_consecutivas_atuais',
                'faltas_no_trimestre',
                'total_faltas',
                'atrasos_no_trimestre',
                'nome',
            ])
            .subscribe(rows => {
                setAlunos(rows.map(a => ({
                    id: a.id,
                    nome: a.nome,
                    turmaId: a.turmaId,
                    matricula: '',
                    faltasConsecutivasAtuais: a.faltasConsecutivasAtuais ?? 0,
                    faltasNoTrimestre: a.faltasNoTrimestre ?? 0,
                    totalFaltas: a.totalFaltas ?? 0,
                    atrasosNoTrimestre: a.atrasosNoTrimestre ?? 0,
                })));
                setLoading(false);
            });
        return () => subscription.unsubscribe();
    }, [turmaId]);

    const renderItem = ({ item }: { item: AlunoDto }) => (
        <Card
            style={styles.card}
            mode="elevated"
            onPress={() => navigation.navigate('AlunoForm', { turmaId, aluno: item })}
        >
            <Card.Content>
                <View style={styles.cardTop}>
                    <View style={styles.avatarCircle}>
                        <Text variant="titleMedium" style={styles.avatarText}>
                            {item.nome.charAt(0).toUpperCase()}
                        </Text>
                    </View>
                    <View style={styles.cardInfo}>
                        <Text variant="titleMedium" style={styles.cardTitle}>{item.nome}</Text>
                        <Text variant="bodySmall" style={styles.cardSubtitle}>
                            Matrícula: {item.matricula || 'N/A'}
                        </Text>
                    </View>
                </View>
                <View style={styles.statsRow}>
                    <StatBadge icon="alert-circle" label="Seq." value={item.faltasConsecutivasAtuais} color={theme.colors.error} />
                    <StatBadge icon="calendar-remove" label="Trim." value={item.faltasNoTrimestre} color={theme.colors.warning} />
                    <StatBadge icon="clock-alert" label="Atrasos" value={item.atrasosNoTrimestre} color={theme.colors.info} />
                </View>
            </Card.Content>
        </Card>
    );

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Alunos"
                subtitle={turmaNome}
                onBack={() => navigation.goBack()}
                rightActions={[{ icon: 'account-plus', onPress: () => navigation.navigate('AlunoForm', { turmaId }), label: 'Novo aluno' }]}
            />

            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                </View>
            ) : (
                <FlatList
                    data={alunos}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={
                        <EmptyState
                            icon="account-school"
                            title="Nenhum aluno cadastrado"
                            subtitle="Toque no + para adicionar alunos"
                        />
                    }
                />
            )}

            <FAB
                icon="clipboard-check-outline"
                style={[styles.fab, { bottom: Math.max(insets.bottom + 16, 24) }]}
                onPress={() => navigation.navigate('ChamadaOperacao', { turmaId, turmaNome })}
                label="Fazer Chamada"
                color={theme.colors.surface}
            />
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    listContainer: { padding: theme.spacing.md, paddingBottom: 100 },
    card: {
        marginBottom: theme.spacing.sm + 4,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
    },
    cardTop: { flexDirection: 'row', alignItems: 'center', marginBottom: theme.spacing.sm + 4 },
    avatarCircle: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: theme.colors.primary + '15',
        alignItems: 'center',
        justifyContent: 'center',
        marginRight: theme.spacing.md,
    },
    avatarText: { color: theme.colors.primary, fontWeight: 'bold' },
    cardInfo: { flex: 1 },
    cardTitle: { fontWeight: 'bold', color: theme.colors.textPrimary },
    cardSubtitle: { color: theme.colors.textSecondary, marginTop: 2 },
    statsRow: { flexDirection: 'row', gap: theme.spacing.sm },
    statBadge: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: theme.spacing.sm + 2,
        paddingVertical: theme.spacing.xs,
        borderRadius: theme.borderRadius.full,
    },
    fab: {
        position: 'absolute',
        right: theme.spacing.md,
        backgroundColor: theme.colors.secondary,
        borderRadius: theme.borderRadius.lg,
    },
});
