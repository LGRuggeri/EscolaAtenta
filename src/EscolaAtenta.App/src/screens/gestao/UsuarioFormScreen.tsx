import React, { useState, useCallback, useEffect } from 'react';
import { ScrollView, View, StyleSheet, Alert } from 'react-native';
import { TextInput, Button, Text, Surface, RadioButton, ActivityIndicator, Portal, Modal } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import * as Clipboard from 'expo-clipboard';
import { AppHeader } from '../../components/ui';
import { theme, palette } from '../../theme/colors';
import { PapelUsuario } from '../../types/enums';
import { usuariosService, UsuarioCriadoResult } from '../../services/usuariosService';
import { RootStackParamList } from '../../navigation/types';

const PAPEIS = [
    { valor: PapelUsuario.Monitor, label: 'Monitor', icon: 'eye-outline' },
    { valor: PapelUsuario.Supervisao, label: 'Supervisão / Diretoria', icon: 'shield-account' },
    { valor: PapelUsuario.Administrador, label: 'Administrador', icon: 'cog' },
] as const;

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

    const [resultado, setResultado] = useState<UsuarioCriadoResult | null>(null);
    const [copiado, setCopiado] = useState(false);

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

    const handleCriar = useCallback(async () => {
        if (!nome.trim()) { Alert.alert('Atenção', 'O nome completo é obrigatório.'); return; }
        if (!email.trim()) { Alert.alert('Atenção', 'O e-mail é obrigatório.'); return; }
        try {
            setLoading(true);
            const result = await usuariosService.criarUsuario({ nome: nome.trim(), email: email.trim(), papel });
            setResultado(result);
        } catch (error: any) {
            const msg = error?.response?.data?.message || error?.response?.data || 'Ocorreu um erro ao criar o usuário.';
            Alert.alert('Erro', typeof msg === 'string' ? msg : JSON.stringify(msg));
        } finally {
            setLoading(false);
        }
    }, [nome, email, papel]);

    const handleAtualizar = useCallback(async () => {
        if (!editId) return;
        if (!nome.trim()) { Alert.alert('Atenção', 'O nome completo é obrigatório.'); return; }
        try {
            setLoading(true);
            await usuariosService.atualizarUsuario(editId, { nome: nome.trim(), papel });
            Alert.alert('Sucesso', 'Usuário atualizado com sucesso.', [
                { text: 'OK', onPress: () => navigation.goBack() },
            ]);
        } catch (error: any) {
            const msg = error?.response?.data?.message || error?.response?.data?.detail || 'Ocorreu um erro ao atualizar o usuário.';
            Alert.alert('Erro', typeof msg === 'string' ? msg : JSON.stringify(msg));
        } finally {
            setLoading(false);
        }
    }, [editId, nome, papel, navigation]);

    const handleSalvar = isEdicao ? handleAtualizar : handleCriar;

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

    if (loadingDados) {
        return (
            <SafeAreaView style={styles.container} edges={['top']}>
                <AppHeader title="Editar Usuário" onBack={() => navigation.goBack()} />
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                    <Text variant="bodyMedium" style={styles.loadingText}>Carregando dados...</Text>
                </View>
            </SafeAreaView>
        );
    }

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title={isEdicao ? 'Editar Usuário' : 'Novo Usuário'}
                onBack={() => navigation.goBack()}
            />

            <ScrollView contentContainerStyle={styles.form}>
                <TextInput
                    label="Nome Completo *"
                    placeholder="Ex: Maria da Silva"
                    value={nome}
                    onChangeText={setNome}
                    mode="outlined"
                    left={<TextInput.Icon icon="account" />}
                    disabled={loading}
                    style={styles.input}
                />

                <TextInput
                    label={`E-mail ${isEdicao ? '' : '*'}`}
                    placeholder="Ex: maria@escola.edu.br"
                    value={email}
                    onChangeText={setEmail}
                    keyboardType="email-address"
                    autoCapitalize="none"
                    mode="outlined"
                    left={<TextInput.Icon icon="email" />}
                    disabled={isEdicao || loading}
                    style={styles.input}
                />

                <Text variant="labelLarge" style={styles.sectionLabel}>Papel do Usuário *</Text>
                <RadioButton.Group onValueChange={(v) => setPapel(Number(v) as PapelUsuario)} value={String(papel)}>
                    {PAPEIS.map((item) => (
                        <Surface key={item.valor} style={[styles.radioCard, papel === item.valor && styles.radioCardSelected]} elevation={papel === item.valor ? 2 : 0}>
                            <RadioButton.Item
                                label={item.label}
                                value={String(item.valor)}
                                disabled={loading}
                                labelStyle={styles.radioLabel}
                            />
                        </Surface>
                    ))}
                </RadioButton.Group>

                <Button
                    mode="contained"
                    onPress={handleSalvar}
                    loading={loading}
                    disabled={loading}
                    icon={isEdicao ? 'content-save' : 'account-plus'}
                    style={styles.saveButton}
                    contentStyle={styles.saveButtonContent}
                >
                    {isEdicao ? 'Salvar Alterações' : 'Criar Usuário'}
                </Button>
            </ScrollView>

            {/* Modal de Senha Gerada */}
            <Portal>
                <Modal visible={!!resultado} onDismiss={() => {}} contentContainerStyle={styles.modalContent}>
                    <View style={styles.modalIconCircle}>
                        <MaterialCommunityIcons name="check" size={32} color={palette.white} />
                    </View>
                    <Text variant="titleLarge" style={styles.modalTitle}>Usuário criado!</Text>
                    <Text variant="bodyMedium" style={styles.modalSubtitle}>
                        Uma senha inicial foi gerada automaticamente. Copie e entregue ao novo usuário.
                    </Text>

                    <Surface style={styles.senhaBox} elevation={0}>
                        <Text variant="labelSmall" style={styles.senhaLabel}>SENHA INICIAL</Text>
                        <Text variant="headlineMedium" style={styles.senhaValor} selectable>
                            {resultado?.senhaInicial}
                        </Text>
                    </Surface>

                    <Button
                        mode="contained"
                        onPress={handleCopiarSenha}
                        icon={copiado ? 'check' : 'content-copy'}
                        buttonColor={copiado ? theme.colors.success : theme.colors.primary}
                        style={styles.copiarButton}
                    >
                        {copiado ? 'Copiada!' : 'Copiar Senha'}
                    </Button>

                    <Text variant="labelSmall" style={styles.avisoText}>
                        Esta senha não será exibida novamente.
                    </Text>

                    <Button mode="outlined" onPress={handleConcluir} style={styles.concluirButton}>
                        Concluir
                    </Button>
                </Modal>
            </Portal>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { color: theme.colors.textSecondary, marginTop: theme.spacing.md },
    form: { padding: theme.spacing.lg },
    input: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    sectionLabel: { color: theme.colors.textSecondary, marginBottom: theme.spacing.sm },
    radioCard: {
        borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.sm,
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
    },
    radioCardSelected: { borderColor: theme.colors.primary },
    radioLabel: { fontSize: 15 },
    saveButton: { marginTop: theme.spacing.md, borderRadius: theme.borderRadius.md },
    saveButtonContent: { paddingVertical: theme.spacing.xs },
    modalContent: {
        backgroundColor: theme.colors.surface,
        margin: theme.spacing.lg,
        padding: theme.spacing.lg + 4,
        borderRadius: theme.borderRadius.xl,
        alignItems: 'center',
    },
    modalIconCircle: {
        width: 56, height: 56, borderRadius: 28,
        backgroundColor: theme.colors.success,
        alignItems: 'center', justifyContent: 'center',
        marginBottom: theme.spacing.md,
    },
    modalTitle: { fontWeight: 'bold', color: theme.colors.textPrimary, marginBottom: theme.spacing.sm },
    modalSubtitle: { color: theme.colors.textSecondary, textAlign: 'center', marginBottom: theme.spacing.lg, lineHeight: 20 },
    senhaBox: {
        backgroundColor: theme.colors.surfaceVariant,
        borderRadius: theme.borderRadius.md,
        padding: theme.spacing.md,
        width: '100%',
        alignItems: 'center',
        marginBottom: theme.spacing.md,
    },
    senhaLabel: { color: theme.colors.textMuted, letterSpacing: 1, marginBottom: theme.spacing.sm },
    senhaValor: { fontWeight: 'bold', color: theme.colors.textPrimary, letterSpacing: 2 },
    copiarButton: { width: '100%', borderRadius: theme.borderRadius.sm, marginBottom: theme.spacing.sm },
    avisoText: { color: theme.colors.error, fontWeight: '600', marginBottom: theme.spacing.md },
    concluirButton: { width: '100%', borderRadius: theme.borderRadius.sm },
});
