import React, { useState, useEffect } from 'react';
import { View, StyleSheet, Alert, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { TextInput, Button, Text, IconButton, Surface } from 'react-native-paper';
import { LinearGradient } from 'expo-linear-gradient';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { useAuth } from '../../hooks/useAuth';
import { theme, palette } from '../../theme/colors';
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
            Alert.alert('Servidor não configurado', 'Configure o endereço do servidor antes de fazer login.', [
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
                setTimeout(() => reject(new Error('Timeout: O servidor demorou muito para responder. Verifique se o backend está rodando.')), 10000)
            );

            await Promise.race([loginPromise, timeoutPromise]);
        } catch (err: unknown) {
            let mensagem = 'Falha de comunicação com o servidor. Verifique sua conexão e se o backend está rodando.';

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
        <LinearGradient
            colors={[palette.navy, palette.navyLight, palette.charcoal]}
            style={styles.gradient}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
        >
            <KeyboardAvoidingView
                style={styles.flex}
                behavior={Platform.OS === 'ios' ? 'padding' : undefined}
            >
                <ScrollView
                    contentContainerStyle={styles.scrollContent}
                    keyboardShouldPersistTaps="handled"
                >
                    {/* Settings button */}
                    <IconButton
                        icon="cog-outline"
                        iconColor={palette.gray300}
                        size={26}
                        style={styles.settingsButton}
                        onPress={() => navigation.navigate('ConfiguracaoServidor')}
                    />

                    {/* Logo / Branding */}
                    <View style={styles.brandingContainer}>
                        <View style={styles.logoCircle}>
                            <MaterialCommunityIcons name="school" size={44} color={palette.navy} />
                        </View>
                        <Text variant="headlineLarge" style={styles.title}>EscolaAtenta</Text>
                        <Text variant="bodyLarge" style={styles.subtitle}>
                            Monitoramento de frequência escolar
                        </Text>
                    </View>

                    {/* Login Card */}
                    <Surface style={styles.card} elevation={4}>
                        {!servidorConfigurado && (
                            <Surface
                                style={styles.alertBox}
                                onTouchEnd={() => navigation.navigate('ConfiguracaoServidor')}
                            >
                                <MaterialCommunityIcons name="alert-circle" size={20} color={theme.colors.error} />
                                <Text variant="bodySmall" style={styles.alertText}>
                                    Servidor não configurado. Toque aqui para configurar.
                                </Text>
                            </Surface>
                        )}

                        <TextInput
                            label="E-mail"
                            value={email}
                            onChangeText={setEmail}
                            autoCapitalize="none"
                            keyboardType="email-address"
                            disabled={loading}
                            left={<TextInput.Icon icon="email-outline" />}
                            mode="outlined"
                            outlineStyle={styles.inputOutline}
                            style={styles.input}
                        />

                        <TextInput
                            label="Senha"
                            value={senha}
                            onChangeText={setSenha}
                            secureTextEntry={!mostrarSenha}
                            disabled={loading}
                            left={<TextInput.Icon icon="lock-outline" />}
                            right={
                                <TextInput.Icon
                                    icon={mostrarSenha ? 'eye-off' : 'eye'}
                                    onPress={() => setMostrarSenha(!mostrarSenha)}
                                />
                            }
                            mode="outlined"
                            outlineStyle={styles.inputOutline}
                            style={styles.input}
                        />

                        <Button
                            mode="contained"
                            onPress={handleLogin}
                            loading={loading}
                            disabled={loading}
                            style={styles.loginButton}
                            contentStyle={styles.loginButtonContent}
                            labelStyle={styles.loginButtonLabel}
                            icon="login"
                        >
                            ENTRAR
                        </Button>
                    </Surface>

                    {/* Footer */}
                    <Text variant="bodySmall" style={styles.footerText}>
                        Prevenção de evasão escolar
                    </Text>
                </ScrollView>
            </KeyboardAvoidingView>
        </LinearGradient>
    );
}

const styles = StyleSheet.create({
    flex: { flex: 1 },
    gradient: { flex: 1 },
    scrollContent: {
        flexGrow: 1,
        justifyContent: 'center',
        padding: theme.spacing.lg,
    },
    settingsButton: {
        position: 'absolute',
        top: 48,
        right: 8,
    },
    brandingContainer: {
        alignItems: 'center',
        marginBottom: theme.spacing.xl,
    },
    logoCircle: {
        width: 80,
        height: 80,
        borderRadius: 40,
        backgroundColor: palette.white,
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: theme.spacing.md,
        ...theme.shadow.lg,
    },
    title: {
        color: palette.white,
        fontWeight: 'bold',
    },
    subtitle: {
        color: palette.gray300,
        marginTop: theme.spacing.xs,
    },
    card: {
        backgroundColor: palette.white,
        borderRadius: theme.borderRadius.xl,
        padding: theme.spacing.lg,
        ...theme.shadow.lg,
    },
    alertBox: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: theme.colors.errorLight,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.md,
        gap: theme.spacing.sm,
    },
    alertText: {
        color: theme.colors.error,
        flex: 1,
    },
    input: {
        marginBottom: theme.spacing.md,
        backgroundColor: palette.white,
    },
    inputOutline: {
        borderRadius: theme.borderRadius.sm,
    },
    loginButton: {
        marginTop: theme.spacing.sm,
        borderRadius: theme.borderRadius.sm,
    },
    loginButtonContent: {
        paddingVertical: theme.spacing.sm,
    },
    loginButtonLabel: {
        fontSize: 16,
        fontWeight: 'bold',
        letterSpacing: 1,
    },
    footerText: {
        color: palette.gray500,
        textAlign: 'center',
        marginTop: theme.spacing.lg,
    },
});
