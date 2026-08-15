import React, { useState, useEffect, useCallback } from 'react';
import { View, StyleSheet, FlatList, Alert, Pressable } from 'react-native';
import { Text, Button, Surface, TextInput } from 'react-native-paper';
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

function formatarDataBrasil(data: Date): string {
    const dia = String(data.getDate()).padStart(2, '0');
    const mes = String(data.getMonth() + 1).padStart(2, '0');
    const ano = data.getFullYear();
    return `${dia}/${mes}/${ano}`;
}

function parseDataBrasil(texto: string): Date | null {
    const partes = texto.split('/');
    if (partes.length !== 3) return null;

    const dia = parseInt(partes[0], 10);
    const mes = parseInt(partes[1], 10) - 1;
    const ano = parseInt(partes[2], 10);

    if (Number.isNaN(dia) || Number.isNaN(mes) || Number.isNaN(ano)) return null;

    const data = new Date(ano, mes, dia);
    if (data.getFullYear() !== ano || data.getMonth() !== mes || data.getDate() !== dia) return null;

    return data;
}

function inicioDoDia(data: Date): Date {
    return new Date(data.getFullYear(), data.getMonth(), data.getDate());
}

function fimDoDia(data: Date): Date {
    return new Date(data.getFullYear(), data.getMonth(), data.getDate() + 1);
}

function ChamadaScreenRaw({ route, navigation, alunos }: ChamadaScreenProps) {
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [dataTexto, setDataTexto] = useState(formatarDataBrasil(new Date()));
    const [dataSelecionada, setDataSelecionada] = useState(new Date());
    const [statusMap, setStatusMap] = useState<Record<string, StatusPresencaLocal>>({});
    const [somenteLeitura, setSomenteLeitura] = useState(false);
    const [modoEdicao, setModoEdicao] = useState(false);

    useEffect(() => {
        if (alunos.length > 0 && !somenteLeitura && Object.keys(statusMap).length === 0) {
            const initialMap: Record<string, StatusPresencaLocal> = {};
            alunos.forEach((a) => {
                initialMap[a.id] = 'Presente';
            });
            setStatusMap(initialMap);
        }
    }, [alunos, somenteLeitura]);

    const setStatus = (alunoId: string, status: StatusPresencaLocal) => {
        if (somenteLeitura) return;
        Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
        setStatusMap((prev) => ({ ...prev, [alunoId]: status }));
    };

    const carregarRegistrosExistentes = useCallback(async (data: Date) => {
        const registrosCollection = database.get<RegistroPresenca>('registros_presenca');
        const registros = await registrosCollection
            .query(Q.where('turma_id', turmaId))
            .fetch();

        const inicio = inicioDoDia(data).getTime();
        const fim = fimDoDia(data).getTime();

        return registros.filter((r) => {
            const t = r.data.getTime();
            return t >= inicio && t < fim;
        });
    }, [turmaId]);

    const aplicarStatusDosRegistros = (registros: RegistroPresenca[]) => {
        const novoMap: Record<string, StatusPresencaLocal> = {};
        registros.forEach((r) => {
            if (STATUS_OPTIONS.some((opt) => opt.key === r.status)) {
                novoMap[r.alunoId] = r.status;
            }
        });
        // Mantém os alunos sem registro como Presente
        alunos.forEach((a) => {
            if (!(a.id in novoMap)) {
                novoMap[a.id] = 'Presente';
            }
        });
        setStatusMap(novoMap);
    };

    const handleDataChange = (texto: string) => {
        setDataTexto(texto);
        const data = parseDataBrasil(texto);
        if (data) {
            setDataSelecionada(data);
            setSomenteLeitura(false);
            setModoEdicao(false);
            setStatusMap({}); // Reseta para recarregar padrões
        }
    };

    const handleHoje = () => {
        const hoje = new Date();
        setDataTexto(formatarDataBrasil(hoje));
        setDataSelecionada(hoje);
        setSomenteLeitura(false);
        setModoEdicao(false);
        setStatusMap({});
    };

    const salvarNovosRegistros = async () => {
        const registrosCollection = database.get<RegistroPresenca>('registros_presenca');

        await database.write(async () => {
            const batch = alunos.map((aluno) =>
                registrosCollection.prepareCreate((record) => {
                    record.alunoId = aluno.id;
                    record.turmaId = turmaId;
                    record.data = dataSelecionada;
                    record.status = statusMap[aluno.id] ?? 'Presente';
                    record.sincronizado = false;
                })
            );

            await database.batch(...batch);
        });
    };

    const atualizarRegistrosExistentes = async (registros: RegistroPresenca[]) => {
        const registrosPorAluno = new Map<string, RegistroPresenca>();
        registros.forEach((r) => registrosPorAluno.set(r.alunoId, r));

        await database.write(async () => {
            const batch: RegistroPresenca[] = [];
            alunos.forEach((aluno) => {
                const registro = registrosPorAluno.get(aluno.id);
                if (registro) {
                    const novoStatus = statusMap[aluno.id] ?? 'Presente';
                    if (registro.status !== novoStatus) {
                        batch.push(
                            registro.prepareUpdate((record) => {
                                record.status = novoStatus;
                                record.sincronizado = false;
                            })
                        );
                    }
                }
            });

            if (batch.length > 0) {
                await database.batch(...batch);
            }
        });
    };

    const finalizarSalvamento = async () => {
        Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
        setSomenteLeitura(true);
        setModoEdicao(false);

        syncWithServer().catch(() => {});
    };

    const handleSalvar = async () => {
        if (alunos.length === 0) {
            Alert.alert('Aviso', 'Não há alunos nesta turma para registrar chamada.');
            return;
        }

        if (somenteLeitura) {
            // Sai do modo visualização e permite edição
            setSomenteLeitura(false);
            setModoEdicao(true);
            return;
        }

        try {
            const registrosExistentes = await carregarRegistrosExistentes(dataSelecionada);

            if (registrosExistentes.length > 0 && !modoEdicao) {
                aplicarStatusDosRegistros(registrosExistentes);
                Alert.alert(
                    'Chamada já realizada',
                    `Já existe uma chamada para o dia ${dataTexto}. O que deseja fazer?`,
                    [
                        { text: 'Cancelar', style: 'cancel' },
                        {
                            text: 'Visualizar',
                            onPress: () => {
                                setSomenteLeitura(true);
                                setModoEdicao(false);
                            },
                        },
                        {
                            text: 'Atualizar',
                            onPress: async () => {
                                setModoEdicao(true);
                                await atualizarRegistrosExistentes(registrosExistentes);
                                await finalizarSalvamento();
                            },
                        },
                    ],
                    { cancelable: false }
                );
                return;
            }

            if (registrosExistentes.length > 0 && modoEdicao) {
                await atualizarRegistrosExistentes(registrosExistentes);
            } else {
                await salvarNovosRegistros();
            }

            await finalizarSalvamento();
        } catch (error) {
            Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
            console.error('[CHAMADA] Erro ao salvar localmente:', error);
            Alert.alert('Erro', 'Falha ao salvar a chamada no dispositivo.');
        }
    };

    const renderItem = ({ item }: { item: Aluno }) => {
        const currentStatus = statusMap[item.id] ?? 'Presente';

        return (
            <Surface style={[styles.card, somenteLeitura && styles.cardDesabilitado]} elevation={1}>
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
                                    somenteLeitura && !isActive && styles.statusButtonDesabilitado,
                                ]}
                                onPress={() => setStatus(item.id, opt.key)}
                                disabled={somenteLeitura}
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

    const tituloBotao = somenteLeitura ? 'Editar Chamada' : modoEdicao ? 'Atualizar Chamada' : 'Salvar Chamada';

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Chamada"
                subtitle={turmaNome}
                onBack={() => navigation.goBack()}
            />

            {/* Seletor de data */}
            <View style={styles.dataRow}>
                <TextInput
                    label="Data da chamada"
                    value={dataTexto}
                    onChangeText={handleDataChange}
                    mode="outlined"
                    style={styles.dataInput}
                    keyboardType="numeric"
                    placeholder="DD/MM/AAAA"
                    disabled={somenteLeitura}
                />
                <Button
                    mode="outlined"
                    onPress={handleHoje}
                    style={styles.hojeButton}
                    disabled={somenteLeitura}
                >
                    Hoje
                </Button>
            </View>

            {somenteLeitura && (
                <View style={styles.badgeLeitura}>
                    <MaterialCommunityIcons name="eye" size={16} color={theme.colors.info} />
                    <Text variant="labelMedium" style={styles.textoLeitura}>
                        Modo visualização — selecione outra data ou toque em "Editar" para alterar
                    </Text>
                </View>
            )}

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
                    icon={somenteLeitura ? 'pencil' : 'content-save-check'}
                    style={styles.saveButton}
                    contentStyle={styles.saveButtonContent}
                    labelStyle={styles.saveButtonLabel}
                >
                    {tituloBotao}
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
    dataRow: {
        flexDirection: 'row',
        alignItems: 'center',
        paddingHorizontal: theme.spacing.md,
        paddingTop: theme.spacing.sm,
        gap: theme.spacing.sm,
    },
    dataInput: {
        flex: 1,
    },
    hojeButton: {
        marginTop: 6,
    },
    badgeLeitura: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.xs,
        marginHorizontal: theme.spacing.md,
        marginTop: theme.spacing.sm,
        padding: theme.spacing.sm,
        backgroundColor: theme.colors.infoLight,
        borderRadius: theme.borderRadius.sm,
    },
    textoLeitura: {
        color: theme.colors.info,
        flex: 1,
    },
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
    cardDesabilitado: {
        opacity: 0.7,
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
    statusButtonDesabilitado: {
        borderColor: theme.colors.border,
        backgroundColor: theme.colors.background,
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
