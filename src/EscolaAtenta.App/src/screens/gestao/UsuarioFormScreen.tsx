import React, { useState, useCallback, useEffect } from 'react';
import {
    ScrollView,
    View,
    Text,
    StyleSheet,
    TextInput,
    TouchableOpacity,
    Alert,
    ActivityIndicator,
    Modal,
} from 'react-native';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import * as Clipboard from 'expo-clipboard';
import { theme } from '../../theme/colors';
import { PapelUsuario } from '../../types/enums';
import { usuariosService, UsuarioCriadoResult } from '../../services/usuariosService';
import { RootStackParamList } from '../../navigation/types';

const PAPEIS = [
    { valor: PapelUsuario.Monitor, label: 'Monitor' },
    { valor: PapelUsuario.Supervisao, label: 'Supervisao / Diretoria' },
    { valor: PapelUsuario.Administrador, label: 'Administrador' },
] as const;

// Mapeia string do backend (ex: "Monitor") para o valor numérico do enum
function parsePapelFromString(papelStr: string): PapelUsuario {
    switch (papelStr) {
        case 'Administrador': return PapelUsuario.Administrador;
        case 'Supervisao': return PapelUsuario.Supervisao;
        default: return PapelUsuario.Monitor;
    }
}

type UsuarioFormRouteProp = RouteProp<RootStackParamList, 'UsuarioForm'>;

export function UsuarioFormScreen() {
    const navigation = useNavigation();
    const route = useRoute<UsuarioFormRouteProp>();

    const editId = route.params?.id;
    const isEdicao = !!editId;

    const [nome, setNome] = useState('');
    const [email, setEmail] = useState('');
    const [papel, setPapel] = useState<PapelUsuario>(PapelUsuario.Monitor);
    const [loading, setLoading] = useState(false);
    const [loadingDados, setLoadingDados] = useState(isEdicao);

    // Estado do modal de senha gerada (apenas criação)
    const [resultado, setResultado] = useState<UsuarioCriadoResult | null>(null);
    const [copiado, setCopiado] = useState(false);

    // ── Carregar dados para edição ────────────────────────────────────────────
    useEffect(() => {
        if (!editId) return;

        let cancelled = false;

        (async () => {
            try {
                setLoadingDados(true);
                const usuario = await usuariosService.getUsuarioById(editId);
                if (cancelled) return;

                setNome(usuario.nome);
                setEmail(usuario.email);
                setPapel(parsePapelFromString(usuario.papel));
            } catch (error) {
                if (!cancelled) {
                    Alert.alert('Erro', 'Não foi possível carregar os dados do usuário.');
                    navigation.goBack();
                }
            } finally {
                if (!cancelled) setLoadingDados(false);
            }
        })();

        return () => { cancelled = true; };
    }, [editId, navigation]);

    // ── Criar ─────────────────────────────────────────────────────────────────
    const handleCriar = useCallback(async () => {
        if (!nome.trim()) {
            Alert.alert('Atenção', 'O nome completo é obrigatório.');
            return;
        }
        if (!email.trim()) {
            Alert.alert('Atenção', 'O e-mail é obrigatório.');
            return;
        }

        try {
            setLoading(true);
            const result = await usuariosService.criarUsuario({
                nome: nome.trim(),
                email: email.trim(),
                papel,
            });
            setResultado(result);
        } catch (error: any) {
            const msg =
                error?.response?.data?.message ||
                error?.response?.data ||
                'Ocorreu um erro ao criar o usuário.';
            Alert.alert('Erro', typeof msg === 'string' ? msg : JSON.stringify(msg));
        } finally {
            setLoading(false);
        }
    }, [nome, email, papel]);

    // ── Atualizar ─────────────────────────────────────────────────────────────
    const handleAtualizar = useCallback(async () => {
        if (!editId) return;
        if (!nome.trim()) {
            Alert.alert('Atenção', 'O nome completo é obrigatório.');
            return;
        }

        try {
            setLoading(true);
            await usuariosService.atualizarUsuario(editId, {
                nome: nome.trim(),
                papel,
            });
            Alert.alert('Sucesso', 'Usuário atualizado com sucesso.', [
                { text: 'OK', onPress: () => navigation.goBack() },
            ]);
        } catch (error: any) {
            const msg =
                error?.response?.data?.message ||
                error?.response?.data?.detail ||
                'Ocorreu um erro ao atualizar o usuário.';
            Alert.alert('Erro', typeof msg === 'string' ? msg : JSON.stringify(msg));
        } finally {
            setLoading(false);
        }
    }, [editId, nome, papel, navigation]);

    const handleSalvar = isEdicao ? handleAtualizar : handleCriar;

    // ── Modal senha (só criação) ──────────────────────────────────────────────
    const handleCopiarSenha = useCallback(async () => {
        if (!resultado) return;
        await Clipboard.setStringAsync(resultado.senhaInicial);
        setCopiado(true);
        setTimeout(() => setCopiado(false), 3000);
    }, [resultado]);

    const handleConcluir = useCallback(() => {
        setResultado(null);
        setCopiado(false);
        navigation.goBack();
    }, [navigation]);

    // ── Loading inicial (edição) ──────────────────────────────────────────────
    if (loadingDados) {
        return (
            <SafeAreaView style={styles.container}>
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                        <Text style={styles.backButtonText}>← Voltar</Text>
                    </TouchableOpacity>
                    <Text style={styles.headerTitle}>Editar Usuário</Text>
                </View>
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" color={theme.colors.primary} />
                    <Text style={styles.loadingText}>Carregando dados...</Text>
                </View>
            </SafeAreaView>
        );
    }

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>
                    {isEdicao ? 'Editar Usuário' : 'Novo Usuário'}
                </Text>
            </View>

            <ScrollView contentContainerStyle={styles.form}>
                <Text style={styles.label}>Nome Completo *</Text>
                <TextInput
                    style={styles.input}
                    placeholder="Ex: Maria da Silva"
                    placeholderTextColor={theme.colors.textSecondary}
                    value={nome}
                    onChangeText={setNome}
                    editable={!loading}
                />

                <Text style={styles.label}>E-mail {isEdicao ? '' : '*'}</Text>
                <TextInput
                    style={[styles.input, isEdicao && styles.inputDisabled]}
                    placeholder="Ex: maria@escola.edu.br"
                    placeholderTextColor={theme.colors.textSecondary}
                    value={email}
                    onChangeText={setEmail}
                    keyboardType="email-address"
                    autoCapitalize="none"
                    editable={!isEdicao && !loading}
                />

                <Text style={styles.label}>Papel do Usuário *</Text>
                <View style={styles.papelGroup}>
                    {PAPEIS.map((item) => {
                        const selecionado = papel === item.valor;
                        return (
                            <TouchableOpacity
                                key={item.valor}
                                style={[styles.papelOption, selecionado && styles.papelOptionSelecionado]}
                                onPress={() => setPapel(item.valor)}
                                disabled={loading}
                            >
                                <Text
                                    style={[
                                        styles.papelOptionText,
                                        selecionado && styles.papelOptionTextSelecionado,
                                    ]}
                                >
                                    {item.label}
                                </Text>
                            </TouchableOpacity>
                        );
                    })}
                </View>

                <TouchableOpacity
                    style={[styles.saveButton, loading && styles.saveButtonDisabled]}
                    onPress={handleSalvar}
                    disabled={loading}
                >
                    {loading ? (
                        <ActivityIndicator color={theme.colors.surface} />
                    ) : (
                        <Text style={styles.saveButtonText}>
                            {isEdicao ? 'Salvar Alterações' : 'Criar Usuário'}
                        </Text>
                    )}
                </TouchableOpacity>
            </ScrollView>

            {/* Modal de Senha Gerada — apenas no fluxo de criação */}
            <Modal visible={!!resultado} transparent animationType="fade">
                <View style={styles.modalOverlay}>
                    <View style={styles.modalCard}>
                        <View style={styles.modalIconContainer}>
                            <Text style={styles.modalIcon}>✓</Text>
                        </View>

                        <Text style={styles.modalTitle}>Usuário criado com sucesso!</Text>
                        <Text style={styles.modalSubtitle}>
                            Uma senha inicial foi gerada automaticamente. Copie e entregue ao
                            novo usuário.
                        </Text>

                        <View style={styles.senhaContainer}>
                            <Text style={styles.senhaLabel}>Senha Inicial</Text>
                            <Text style={styles.senhaValor} selectable>
                                {resultado?.senhaInicial}
                            </Text>
                        </View>

                        <TouchableOpacity
                            style={[styles.copiarButton, copiado && styles.copiarButtonCopiado]}
                            onPress={handleCopiarSenha}
                        >
                            <Text style={styles.copiarButtonText}>
                                {copiado ? 'Copiada!' : 'Copiar Senha'}
                            </Text>
                        </TouchableOpacity>

                        <Text style={styles.modalAviso}>
                            Atenção: esta senha não será exibida novamente.
                        </Text>

                        <TouchableOpacity style={styles.concluirButton} onPress={handleConcluir}>
                            <Text style={styles.concluirButtonText}>Concluir</Text>
                        </TouchableOpacity>
                    </View>
                </View>
            </Modal>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: theme.colors.background,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        padding: 20,
        paddingTop: 20,
        backgroundColor: theme.colors.surface,
        elevation: 2,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 },
        shadowOpacity: 0.1,
        shadowRadius: 2,
    },
    backButton: {
        marginRight: 16,
    },
    backButtonText: {
        fontSize: 16,
        color: theme.colors.primary,
        fontWeight: '600',
    },
    headerTitle: {
        fontSize: 20,
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
    },
    form: {
        padding: 20,
    },
    label: {
        fontSize: 14,
        fontWeight: '600',
        color: theme.colors.textSecondary,
        marginBottom: 8,
    },
    input: {
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderRadius: 8,
        padding: 12,
        fontSize: 16,
        marginBottom: 20,
        color: theme.colors.textPrimary,
    },
    inputDisabled: {
        backgroundColor: '#e9ecef',
        color: theme.colors.textSecondary,
    },

    // Loading
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { marginTop: 12, color: theme.colors.textSecondary, fontSize: 14 },

    // Toggle de papéis
    papelGroup: {
        flexDirection: 'column',
        gap: 10,
        marginBottom: 24,
    },
    papelOption: {
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderRadius: 8,
        padding: 14,
        backgroundColor: theme.colors.surface,
        alignItems: 'center',
    },
    papelOptionSelecionado: {
        borderColor: theme.colors.primary,
        backgroundColor: theme.colors.primary,
    },
    papelOptionText: {
        fontSize: 15,
        fontWeight: '600',
        color: theme.colors.textPrimary,
    },
    papelOptionTextSelecionado: {
        color: theme.colors.surface,
    },

    // Botão salvar
    saveButton: {
        backgroundColor: theme.colors.primary,
        padding: 16,
        borderRadius: 12,
        alignItems: 'center',
        marginTop: 12,
    },
    saveButtonDisabled: {
        opacity: 0.7,
    },
    saveButtonText: {
        color: theme.colors.surface,
        fontSize: 16,
        fontWeight: 'bold',
    },

    // Modal
    modalOverlay: {
        flex: 1,
        backgroundColor: 'rgba(0,0,0,0.5)',
        justifyContent: 'center',
        alignItems: 'center',
        padding: 24,
    },
    modalCard: {
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        padding: 28,
        width: '100%',
        alignItems: 'center',
    },
    modalIconContainer: {
        width: 56,
        height: 56,
        borderRadius: 28,
        backgroundColor: theme.colors.secondary,
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: 16,
    },
    modalIcon: {
        fontSize: 28,
        color: theme.colors.surface,
        fontWeight: 'bold',
    },
    modalTitle: {
        fontSize: 20,
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        textAlign: 'center',
        marginBottom: 8,
    },
    modalSubtitle: {
        fontSize: 14,
        color: theme.colors.textSecondary,
        textAlign: 'center',
        marginBottom: 20,
        lineHeight: 20,
    },
    senhaContainer: {
        backgroundColor: theme.colors.background,
        borderRadius: 12,
        padding: 16,
        width: '100%',
        alignItems: 'center',
        marginBottom: 16,
    },
    senhaLabel: {
        fontSize: 12,
        fontWeight: '600',
        color: theme.colors.textSecondary,
        marginBottom: 8,
        textTransform: 'uppercase',
        letterSpacing: 1,
    },
    senhaValor: {
        fontSize: 24,
        fontWeight: 'bold',
        fontFamily: 'monospace',
        color: theme.colors.textPrimary,
        letterSpacing: 2,
    },

    // Botão copiar
    copiarButton: {
        backgroundColor: theme.colors.primary,
        paddingVertical: 14,
        paddingHorizontal: 32,
        borderRadius: 10,
        width: '100%',
        alignItems: 'center',
        marginBottom: 12,
    },
    copiarButtonCopiado: {
        backgroundColor: theme.colors.secondary,
    },
    copiarButtonText: {
        color: theme.colors.surface,
        fontSize: 16,
        fontWeight: 'bold',
    },

    // Aviso
    modalAviso: {
        fontSize: 12,
        color: theme.colors.error,
        textAlign: 'center',
        marginBottom: 16,
        fontWeight: '600',
    },

    // Botão concluir
    concluirButton: {
        borderWidth: 1,
        borderColor: theme.colors.border,
        paddingVertical: 14,
        paddingHorizontal: 32,
        borderRadius: 10,
        width: '100%',
        alignItems: 'center',
    },
    concluirButtonText: {
        color: theme.colors.textSecondary,
        fontSize: 16,
        fontWeight: '600',
    },
});
