import React, { useState, useEffect } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { useAuth } from '../../hooks/useAuth';
import { theme } from '../../theme/colors';
import { loadServerUrl } from '../../services/api';
import { serverConfig } from '../../services/serverConfig';
import { AxiosError } from 'axios';
import { AppNavigationProp } from '../../navigation/types';

export function LoginScreen() {
    const { signIn } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();
    const [email, setEmail] = useState('');
    const [senha, setSenha] = useState('');
    const [loading, setLoading] = useState(false);
    const [mostrarSenha, setMostrarSenha] = useState(false);
    const [servidorConfigurado, setServidorConfigurado] = useState(false);

    useEffect(() => {
        checkServidor();
    }, []);

    // Recarrega quando voltar da tela de configuracao
    useEffect(() => {
        const unsubscribe = navigation.addListener('focus', () => {
            checkServidor();
        });
        return unsubscribe;
    }, [navigation]);

    const checkServidor = async () => {
        const url = await serverConfig.getUrl();
        setServidorConfigurado(!!url);
        if (url) {
            await loadServerUrl();
        }
    };

    const handleLogin = async () => {
        if (!servidorConfigurado) {
            Alert.alert('Servidor nao configurado', 'Configure o endereco do servidor antes de fazer login.', [
                { text: 'Configurar', onPress: () => navigation.navigate('ConfiguracaoServidor') }
            ]);
            return;
        }

        if (!email || !senha) {
            Alert.alert('Erro', 'Preencha todos os campos');
            return;
        }

        setLoading(true);
        try {
            const loginPromise = signIn(email, senha);
            const timeoutPromise = new Promise((_, reject) =>
                setTimeout(() => reject(new Error('Timeout: O servidor demorou muito para responder. Verifique se o backend esta rodando.')), 10000)
            );

            await Promise.race([loginPromise, timeoutPromise]);
        } catch (err: unknown) {
            let mensagem = 'Falha de comunicacao com o servidor. Verifique sua conexao e se o backend esta rodando.';

            if (err instanceof Error && err.message.includes('Timeout')) {
                mensagem = err.message;
            } else if (err && typeof err === 'object' && 'isAxiosError' in err) {
                const axiosError = err as AxiosError<{ detail?: string, message?: string }>;
                const problemDetailMessage = axiosError.response?.data?.detail;
                mensagem = problemDetailMessage || axiosError.response?.data?.message || mensagem;
            }

            Alert.alert('Erro de Acesso', mensagem);
        } finally {
            setLoading(false);
        }
    };

    return (
        <View style={styles.container}>
            {/* Botao engrenagem no canto superior direito */}
            <TouchableOpacity
                style={styles.settingsButton}
                onPress={() => navigation.navigate('ConfiguracaoServidor')}
            >
                <Ionicons name="settings-outline" size={26} color={theme.colors.textSecondary} />
            </TouchableOpacity>

            <Text style={styles.title}>EscolaAtenta</Text>
            <Text style={styles.subtitle}>Faca login para continuar</Text>

            {!servidorConfigurado && (
                <TouchableOpacity
                    style={styles.alertBox}
                    onPress={() => navigation.navigate('ConfiguracaoServidor')}
                >
                    <Ionicons name="warning" size={20} color={theme.colors.error} />
                    <Text style={styles.alertText}>Servidor nao configurado. Toque aqui para configurar.</Text>
                </TouchableOpacity>
            )}

            <TextInput
                style={styles.input}
                placeholder="E-mail"
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                keyboardType="email-address"
                editable={!loading}
            />

            <View style={styles.passwordContainer}>
                <TextInput
                    style={styles.passwordInput}
                    placeholder="Senha"
                    value={senha}
                    onChangeText={setSenha}
                    secureTextEntry={!mostrarSenha}
                    editable={!loading}
                />
                <TouchableOpacity
                    style={styles.eyeIcon}
                    onPress={() => setMostrarSenha(!mostrarSenha)}
                    disabled={loading}
                >
                    <Ionicons
                        name={mostrarSenha ? "eye-off" : "eye"}
                        size={24}
                        color={theme.colors.textSecondary}
                    />
                </TouchableOpacity>
            </View>

            <TouchableOpacity style={styles.button} onPress={handleLogin} disabled={loading}>
                {loading ? (
                    <ActivityIndicator color={theme.colors.surface} />
                ) : (
                    <Text style={styles.buttonText}>ENTRAR</Text>
                )}
            </TouchableOpacity>
        </View>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: theme.colors.background },
    settingsButton: {
        position: 'absolute',
        top: 56,
        right: 24,
        padding: 8,
        zIndex: 1,
    },
    title: { fontSize: 32, fontWeight: 'bold', color: theme.colors.primary, textAlign: 'center', marginBottom: 8 },
    subtitle: { fontSize: 16, color: theme.colors.textSecondary, textAlign: 'center', marginBottom: 32 },
    alertBox: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: '#fce4ec',
        padding: 12,
        borderRadius: 8,
        marginBottom: 16,
        gap: 8,
    },
    alertText: {
        color: theme.colors.error,
        fontSize: 14,
        flex: 1,
    },
    input: { backgroundColor: theme.colors.surface, borderWidth: 1, borderColor: theme.colors.border, borderRadius: 8, padding: 16, marginBottom: 16, fontSize: 16, color: theme.colors.textPrimary },
    passwordContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderRadius: 8,
        marginBottom: 16,
    },
    passwordInput: {
        flex: 1,
        padding: 16,
        fontSize: 16,
        color: theme.colors.textPrimary
    },
    eyeIcon: {
        padding: 16,
    },
    button: { backgroundColor: theme.colors.primary, padding: 16, borderRadius: 8, alignItems: 'center', marginTop: 8 },
    buttonText: { color: theme.colors.surface, fontSize: 16, fontWeight: 'bold' }
});
