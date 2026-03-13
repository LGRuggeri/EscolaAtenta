import React, { useState, useEffect } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { theme } from '../../theme/colors';
import { serverConfig } from '../../services/serverConfig';
import { loadServerUrl } from '../../services/api';
import { AppNavigationProp } from '../../navigation/types';
import database from '../../database';

const PORTA_PADRAO = '5114';

export function ConfiguracaoServidorScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const [ip, setIp] = useState('');
    const [porta, setPorta] = useState(PORTA_PADRAO);
    const [testando, setTestando] = useState(false);
    const [resultado, setResultado] = useState<{ ok: boolean; message: string } | null>(null);

    useEffect(() => {
        serverConfig.getUrl().then((saved) => {
            if (saved) {
                // Extrai IP e porta da URL salva (http://192.168.x.x:5114)
                try {
                    const match = saved.match(/^https?:\/\/([^:]+):(\d+)/);
                    if (match) {
                        setIp(match[1]);
                        setPorta(match[2]);
                    }
                } catch { /* ignora parsing errors */ }
            }
        });
    }, []);

    const buildUrl = () => `http://${ip.trim()}:${porta.trim() || PORTA_PADRAO}`;

    const handleTestar = async () => {
        if (!ip.trim()) {
            Alert.alert('Erro', 'Digite o IP do servidor.');
            return;
        }

        setTestando(true);
        setResultado(null);
        const result = await serverConfig.testConnection(buildUrl());
        setResultado(result);
        setTestando(false);
    };

    const handleLimparBanco = () => {
        Alert.alert(
            'Limpar banco local',
            'Isso apagará todos os dados locais (turmas, alunos, presenças) e sincronizará tudo do servidor novamente. Dados não sincronizados serão perdidos.\n\nDeseja continuar?',
            [
                { text: 'Cancelar', style: 'cancel' },
                {
                    text: 'Limpar e sincronizar',
                    style: 'destructive',
                    onPress: async () => {
                        try {
                            await database.write(async () => {
                                await database.unsafeResetDatabase();
                            });
                            Alert.alert('Banco limpo', 'O banco local foi resetado. O app sincronizará tudo ao abrir novamente.');
                        } catch (err: any) {
                            Alert.alert('Erro', 'Não foi possível limpar o banco: ' + (err?.message ?? 'erro desconhecido'));
                        }
                    }
                },
            ]
        );
    };

    const handleSalvar = async () => {
        if (!ip.trim()) {
            Alert.alert('Erro', 'Digite o IP do servidor.');
            return;
        }

        const url = buildUrl();

        // Testa antes de salvar
        setTestando(true);
        setResultado(null);
        const result = await serverConfig.testConnection(url);
        setResultado(result);
        setTestando(false);

        if (!result.ok) {
            Alert.alert('Falha na conexao', 'Nao foi possivel conectar ao servidor. Deseja salvar mesmo assim?', [
                { text: 'Cancelar', style: 'cancel' },
                {
                    text: 'Salvar mesmo assim',
                    onPress: async () => {
                        await serverConfig.saveUrl(url);
                        await loadServerUrl();
                        navigation.goBack();
                    }
                },
            ]);
            return;
        }

        await serverConfig.saveUrl(url);
        await loadServerUrl();
        Alert.alert('Sucesso', 'Servidor configurado com sucesso!', [
            { text: 'OK', onPress: () => navigation.goBack() }
        ]);
    };

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={theme.colors.primary} />
                </TouchableOpacity>
                <Text style={styles.title}>Configurar Servidor</Text>
            </View>

            <View style={styles.content}>
                <Ionicons name="server-outline" size={48} color={theme.colors.primary} style={styles.icon} />

                <Text style={styles.label}>IP do servidor da escola</Text>
                <Text style={styles.hint}>
                    Digite o IP do computador onde o EscolaAtenta esta instalado.{'\n'}
                    Exemplo: 192.168.1.100
                </Text>

                <TextInput
                    style={styles.input}
                    placeholder="192.168.1.100"
                    value={ip}
                    onChangeText={(text) => {
                        setIp(text);
                        setResultado(null);
                    }}
                    autoCapitalize="none"
                    autoCorrect={false}
                    keyboardType="numeric"
                />

                <Text style={styles.label}>Porta</Text>
                <TextInput
                    style={styles.input}
                    placeholder={PORTA_PADRAO}
                    value={porta}
                    onChangeText={(text) => {
                        setPorta(text);
                        setResultado(null);
                    }}
                    autoCapitalize="none"
                    autoCorrect={false}
                    keyboardType="numeric"
                />

                {resultado && (
                    <View style={[styles.resultBox, resultado.ok ? styles.resultOk : styles.resultError]}>
                        <Ionicons
                            name={resultado.ok ? 'checkmark-circle' : 'close-circle'}
                            size={20}
                            color={resultado.ok ? theme.colors.secondary : theme.colors.error}
                        />
                        <Text style={[styles.resultText, resultado.ok ? styles.resultTextOk : styles.resultTextError]}>
                            {resultado.message}
                        </Text>
                    </View>
                )}

                <TouchableOpacity
                    style={styles.testButton}
                    onPress={handleTestar}
                    disabled={testando}
                >
                    {testando ? (
                        <ActivityIndicator color={theme.colors.primary} />
                    ) : (
                        <>
                            <Ionicons name="wifi" size={18} color={theme.colors.primary} />
                            <Text style={styles.testButtonText}>Testar Conexao</Text>
                        </>
                    )}
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.saveButton}
                    onPress={handleSalvar}
                    disabled={testando}
                >
                    <Text style={styles.saveButtonText}>SALVAR</Text>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.resetButton}
                    onPress={handleLimparBanco}
                >
                    <Ionicons name="trash-outline" size={18} color={theme.colors.error} />
                    <Text style={styles.resetButtonText}>Limpar banco local e ressincronizar</Text>
                </TouchableOpacity>
            </View>
        </View>
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
        paddingTop: 56,
        paddingHorizontal: 16,
        paddingBottom: 16,
        backgroundColor: theme.colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.border,
    },
    backButton: {
        padding: 8,
        marginRight: 8,
    },
    title: {
        fontSize: 20,
        fontWeight: 'bold',
        color: theme.colors.primary,
    },
    content: {
        flex: 1,
        padding: 24,
    },
    icon: {
        alignSelf: 'center',
        marginBottom: 24,
    },
    label: {
        fontSize: 16,
        fontWeight: '600',
        color: theme.colors.textPrimary,
        marginBottom: 8,
    },
    hint: {
        fontSize: 14,
        color: theme.colors.textSecondary,
        marginBottom: 16,
        lineHeight: 20,
    },
    input: {
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderRadius: 8,
        padding: 16,
        fontSize: 16,
        color: theme.colors.textPrimary,
        marginBottom: 16,
    },
    resultBox: {
        flexDirection: 'row',
        alignItems: 'center',
        padding: 12,
        borderRadius: 8,
        marginBottom: 16,
        gap: 8,
    },
    resultOk: {
        backgroundColor: '#e8f5e9',
    },
    resultError: {
        backgroundColor: '#fce4ec',
    },
    resultText: {
        fontSize: 14,
        flex: 1,
    },
    resultTextOk: {
        color: theme.colors.secondary,
    },
    resultTextError: {
        color: theme.colors.error,
    },
    testButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 14,
        borderRadius: 8,
        borderWidth: 1,
        borderColor: theme.colors.primary,
        marginBottom: 12,
        gap: 8,
    },
    testButtonText: {
        color: theme.colors.primary,
        fontSize: 15,
        fontWeight: '600',
    },
    saveButton: {
        backgroundColor: theme.colors.primary,
        padding: 16,
        borderRadius: 8,
        alignItems: 'center',
    },
    saveButtonText: {
        color: theme.colors.surface,
        fontSize: 16,
        fontWeight: 'bold',
    },
    resetButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 14,
        borderRadius: 8,
        borderWidth: 1,
        borderColor: theme.colors.error,
        marginTop: 12,
        gap: 8,
    },
    resetButtonText: {
        color: theme.colors.error,
        fontSize: 14,
        fontWeight: '600',
    },
});
