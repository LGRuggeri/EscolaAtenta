import React, { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../hooks/useAuth';

export function LoginScreen() {
    const { signIn } = useAuth();
    const [email, setEmail] = useState('');
    const [senha, setSenha] = useState('');
    const [loading, setLoading] = useState(false);
    const [mostrarSenha, setMostrarSenha] = useState(false);

    const handleLogin = async () => {
        if (!email || !senha) {
            Alert.alert('Erro', 'Preencha todos os campos');
            return;
        }

        setLoading(true);
        try {
            // Adiciona um timeout manual para evitar que a tela congele infinitamente se a API C# não estiver rodando no IP correto
            const loginPromise = signIn(email, senha);
            const timeoutPromise = new Promise((_, reject) =>
                setTimeout(() => reject(new Error('Timeout: O servidor demorou muito para responder. Verifique se o backend C# está rodando no IP da mesma rede Wi-Fi.')), 10000)
            );

            await Promise.race([loginPromise, timeoutPromise]);
        } catch (error: any) {
            const mensagem = error.message.includes('Timeout')
                ? error.message
                : (error.response?.data?.message || 'Falha de comunicação com o servidor. O Computador (API C#) e o Celular estão na mesma rede Wi-Fi?');
            Alert.alert('Ops!', mensagem);
        } finally {
            setLoading(false);
        }
    };

    return (
        <View style={styles.container}>
            <Text style={styles.title}>EscolaAtenta</Text>
            <Text style={styles.subtitle}>Faça login para continuar</Text>

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
                        color="#666"
                    />
                </TouchableOpacity>
            </View>

            <TouchableOpacity style={styles.button} onPress={handleLogin} disabled={loading}>
                {loading ? (
                    <ActivityIndicator color="#FFF" />
                ) : (
                    <Text style={styles.buttonText}>ENTRAR</Text>
                )}
            </TouchableOpacity>
        </View>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: '#F9F9F9' },
    title: { fontSize: 32, fontWeight: 'bold', color: '#D4AF37', textAlign: 'center', marginBottom: 8 },
    subtitle: { fontSize: 16, color: '#666', textAlign: 'center', marginBottom: 32 },
    input: { backgroundColor: '#FFF', borderWidth: 1, borderColor: '#DDD', borderRadius: 8, padding: 16, marginBottom: 16, fontSize: 16 },
    passwordContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: '#FFF',
        borderWidth: 1,
        borderColor: '#DDD',
        borderRadius: 8,
        marginBottom: 16,
    },
    passwordInput: {
        flex: 1,
        padding: 16,
        fontSize: 16,
    },
    eyeIcon: {
        padding: 16,
    },
    button: { backgroundColor: '#D4AF37', padding: 16, borderRadius: 8, alignItems: 'center', marginTop: 8 },
    buttonText: { color: '#FFF', fontSize: 16, fontWeight: 'bold' }
});
