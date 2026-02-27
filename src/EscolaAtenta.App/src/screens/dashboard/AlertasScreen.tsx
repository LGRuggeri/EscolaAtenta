import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, ActivityIndicator, TouchableOpacity, Alert, Modal, TextInput } from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AlertaDto } from '../../types/dtos';
import { NivelAlertaFalta, PapelUsuario } from '../../types/enums';
import { alertasService } from '../../services/alertasService';
import { useAuth } from '../../hooks/useAuth';

export function AlertasScreen() {
    const { user } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();
    const [alertas, setAlertas] = useState<AlertaDto[]>([]);
    const [loading, setLoading] = useState(true);

    // Estados do Modal
    const [modalVisible, setModalVisible] = useState(false);
    const [alertaSelecionado, setAlertaSelecionado] = useState<AlertaDto | null>(null);
    const [tratativa, setTratativa] = useState('');
    const [resolvendo, setResolvendo] = useState(false);

    useFocusEffect(
        useCallback(() => {
            carregarAlertas();
        }, [])
    );

    async function carregarAlertas() {
        try {
            setLoading(true);
            const data = await alertasService.obterAtivos();
            setAlertas(data);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar os alertas.');
            console.error(error);
        } finally {
            setLoading(false);
        }
    }

    const openModal = (alerta: AlertaDto) => {
        setAlertaSelecionado(alerta);
        setTratativa('');
        setModalVisible(true);
    };

    const closeModal = () => {
        setModalVisible(false);
        setAlertaSelecionado(null);
        setTratativa('');
    };

    const handleResolver = async () => {
        if (!alertaSelecionado) return;
        if (!tratativa.trim()) {
            Alert.alert('Aviso', 'Por favor, informe a tratativa ou ação tomada.');
            return;
        }

        try {
            setResolvendo(true);
            await alertasService.resolver(alertaSelecionado.id, tratativa);

            // Atualização otimista
            setAlertas(prev => prev.filter(a => a.id !== alertaSelecionado.id));
            Alert.alert('Sucesso', 'Alerta resolvido com sucesso!');
            closeModal();
        } catch (error) {
            Alert.alert('Erro', 'Ocorreu um problema ao resolver o alerta.');
            console.error(error);
        } finally {
            setResolvendo(false);
        }
    };

    const getBorderColorByNivel = (nivel: NivelAlertaFalta): string => {
        switch (nivel) {
            case NivelAlertaFalta.Excelencia: return '#10B981'; // Verde
            case NivelAlertaFalta.Aviso: return '#FBBF24'; // Amarelo
            case NivelAlertaFalta.Intermediario: return '#F97316'; // Laranja
            case NivelAlertaFalta.Vermelho: return '#EF4444'; // Vermelho
            case NivelAlertaFalta.Preto: return '#1F2937'; // Preto/Cinza Escuro
            default: return '#E5E7EB';
        }
    };

    const formatData = (isoDate: string) => {
        const date = new Date(isoDate);
        return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    };

    const renderItem = ({ item }: { item: AlertaDto }) => {
        const borderColor = getBorderColorByNivel(item.nivel);

        return (
            <TouchableOpacity
                style={[styles.card, { borderLeftColor: borderColor, borderLeftWidth: 6 }]}
                onPress={() => openModal(item)}
            >
                <View style={styles.cardHeader}>
                    <Text style={[styles.cardTitle, { color: borderColor }]}>
                        {item.tituloAmigavel || `Alerta ${NivelAlertaFalta[item.nivel]}`}
                    </Text>
                    <Text style={styles.cardDate}>{formatData(item.dataAlerta)}</Text>
                </View>
                <Text style={{ fontWeight: 'bold', fontSize: 13, color: '#374151', marginBottom: 2 }}>
                    Aluno: {item.nomeAluno} | {item.nomeTurma}
                </Text>
                <Text style={styles.cardDescricao}>{item.mensagemAcao || item.descricao}</Text>

                <View style={styles.resolverBadge}>
                    <Text style={styles.resolverBadgeText}>Toque para Resolver</Text>
                </View>
            </TouchableOpacity>
        );
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Alertas de Evasão</Text>
                <View style={{ width: 60 }} />
            </View>

            {loading ? (
                <ActivityIndicator size="large" color="#EF4444" style={{ marginTop: 50 }} />
            ) : (
                <FlatList
                    data={alertas}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={<Text style={styles.emptyText}>Nenhuma situação de risco detectada (0 alertas).</Text>}
                />
            )}

            {/* Modal de Resolução */}
            <Modal
                animationType="slide"
                transparent={true}
                visible={modalVisible}
                onRequestClose={closeModal}
            >
                <View style={styles.modalOverlay}>
                    <View style={styles.modalContent}>
                        <Text style={styles.modalTitle}>Resolver Alerta</Text>

                        {alertaSelecionado && (
                            <View style={styles.modalAlertaInfo}>
                                <Text style={styles.modalAlertaDesc}>{alertaSelecionado.mensagemAcao || alertaSelecionado.descricao}</Text>
                            </View>
                        )}

                        {user?.papel === PapelUsuario.Monitor ? (
                            <>
                                <View style={styles.monitorAvisoContainer}>
                                    <Text style={styles.monitorAvisoText}>
                                        📌 Apenas a supervisão pode registrar a tratativa e resolver este alerta.
                                    </Text>
                                </View>
                                <View style={styles.modalActions}>
                                    <TouchableOpacity style={[styles.modalButton, styles.modalButtonCancel]} onPress={closeModal}>
                                        <Text style={styles.modalButtonCancelText}>Fechar</Text>
                                    </TouchableOpacity>
                                </View>
                            </>
                        ) : (
                            <>
                                <Text style={styles.inputLabel}>Tratativa / Ação Tomada:</Text>
                                <TextInput
                                    style={styles.textInput}
                                    multiline
                                    numberOfLines={4}
                                    placeholder="Descreva a ligação feita aos pais, a conversa com o aluno, etc..."
                                    value={tratativa}
                                    onChangeText={setTratativa}
                                />

                                <View style={styles.modalActions}>
                                    <TouchableOpacity style={[styles.modalButton, styles.modalButtonCancel]} onPress={closeModal} disabled={resolvendo}>
                                        <Text style={styles.modalButtonCancelText}>Cancelar</Text>
                                    </TouchableOpacity>
                                    <TouchableOpacity
                                        style={[styles.modalButton, styles.modalButtonConfirm, resolvendo && { opacity: 0.7 }]}
                                        onPress={handleResolver}
                                        disabled={resolvendo}
                                    >
                                        {resolvendo ? <ActivityIndicator color="#FFF" /> : <Text style={styles.modalButtonConfirmText}>Confirmar</Text>}
                                    </TouchableOpacity>
                                </View>
                            </>
                        )}
                    </View>
                </View>
            </Modal>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: '#F9FAFB' },
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', padding: 20, paddingTop: 20, backgroundColor: '#FFF', elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    backButton: {},
    backButtonText: { fontSize: 16, color: '#374151', fontWeight: '600' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: '#111827' },
    listContainer: { padding: 16, paddingBottom: 40 },
    card: { backgroundColor: '#FFF', padding: 16, borderRadius: 8, marginBottom: 12, elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 },
    cardTitle: { fontSize: 16, fontWeight: 'bold' },
    cardDate: { fontSize: 12, color: '#6B7280' },
    cardDescricao: { fontSize: 14, color: '#374151', lineHeight: 20 },
    emptyText: { textAlign: 'center', color: '#10B981', marginTop: 32, fontSize: 16, fontWeight: '600' },
    resolverBadge: { alignSelf: 'flex-start', marginTop: 12, backgroundColor: '#F3F4F6', paddingHorizontal: 12, paddingVertical: 6, borderRadius: 16 },
    resolverBadgeText: { fontSize: 12, color: '#4B5563', fontWeight: '500' },

    // Estilos do Modal
    modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'center', padding: 20 },
    modalContent: { backgroundColor: '#FFF', borderRadius: 12, padding: 24, elevation: 5, shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.25, shadowRadius: 4 },
    modalTitle: { fontSize: 20, fontWeight: 'bold', color: '#111827', marginBottom: 16 },
    modalAlertaInfo: { backgroundColor: '#F9FAFB', padding: 12, borderRadius: 8, marginBottom: 16, borderLeftWidth: 4, borderLeftColor: '#EF4444' },
    modalAlertaDesc: { fontSize: 14, color: '#4B5563', lineHeight: 20 },
    inputLabel: { fontSize: 14, fontWeight: '600', color: '#374151', marginBottom: 8 },
    textInput: { borderWidth: 1, borderColor: '#D1D5DB', borderRadius: 8, padding: 12, fontSize: 14, color: '#111827', textAlignVertical: 'top', minHeight: 100, marginBottom: 20 },
    modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 12 },
    modalButton: { paddingVertical: 10, paddingHorizontal: 16, borderRadius: 8, minWidth: 100, alignItems: 'center' },
    modalButtonCancel: { backgroundColor: '#F3F4F6' },
    modalButtonCancelText: { color: '#4B5563', fontWeight: '600' },
    modalButtonConfirm: { backgroundColor: '#10B981' },
    modalButtonConfirmText: { color: '#FFF', fontWeight: 'bold' },
    monitorAvisoContainer: { backgroundColor: '#FEF3C7', padding: 12, borderRadius: 8, marginBottom: 20 },
    monitorAvisoText: { color: '#92400E', fontSize: 14, lineHeight: 20 }
});
