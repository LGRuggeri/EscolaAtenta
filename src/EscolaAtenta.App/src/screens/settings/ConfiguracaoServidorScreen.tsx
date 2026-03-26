import React, { useState, useEffect } from 'react';
import { View, StyleSheet, Alert, ScrollView } from 'react-native';
import { TextInput, Button, Text, Surface } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppHeader } from '../../components/ui';
import { theme, palette } from '../../theme/colors';
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
        if (!ip.trim()) { Alert.alert('Erro', 'Digite o IP do servidor.'); return; }
        setTestando(true);
        setResultado(null);
        const result = await serverConfig.testConnection(buildUrl());
        setResultado(result);
        setTestando(false);
    };

    const handleLimparBanco = () => {
        Alert.alert(
            'Limpar banco local',
            'Isso apagará todos os dados locais e sincronizará tudo do servidor. Dados não sincronizados serão perdidos.\n\nDeseja continuar?',
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
        if (!ip.trim()) { Alert.alert('Erro', 'Digite o IP do servidor.'); return; }
        const url = buildUrl();
        setTestando(true);
        setResultado(null);
        const result = await serverConfig.testConnection(url);
        setResultado(result);
        setTestando(false);

        if (!result.ok) {
            Alert.alert('Falha na conexão', 'Não foi possível conectar ao servidor. Deseja salvar mesmo assim?', [
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
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Configurar Servidor" onBack={() => navigation.goBack()} />

            <ScrollView contentContainerStyle={styles.content}>
                <View style={styles.iconContainer}>
                    <MaterialCommunityIcons name="server-network" size={48} color={theme.colors.primary} />
                </View>

                <Text variant="bodyMedium" style={styles.hint}>
                    Digite o IP do computador onde o EscolaAtenta está instalado.{'\n'}
                    Exemplo: 192.168.1.100
                </Text>

                <TextInput
                    label="IP do Servidor"
                    placeholder="192.168.1.100"
                    value={ip}
                    onChangeText={(text) => { setIp(text); setResultado(null); }}
                    mode="outlined"
                    left={<TextInput.Icon icon="ip-network" />}
                    keyboardType="numeric"
                    autoCapitalize="none"
                    autoCorrect={false}
                    style={styles.input}
                />

                <TextInput
                    label="Porta"
                    placeholder={PORTA_PADRAO}
                    value={porta}
                    onChangeText={(text) => { setPorta(text); setResultado(null); }}
                    mode="outlined"
                    left={<TextInput.Icon icon="dock-window" />}
                    keyboardType="numeric"
                    autoCapitalize="none"
                    autoCorrect={false}
                    style={styles.input}
                />

                {resultado && (
                    <Surface
                        style={[styles.resultBox, { backgroundColor: resultado.ok ? theme.colors.successLight : theme.colors.errorLight }]}
                        elevation={0}
                    >
                        <MaterialCommunityIcons
                            name={resultado.ok ? 'check-circle' : 'close-circle'}
                            size={20}
                            color={resultado.ok ? theme.colors.success : theme.colors.error}
                        />
                        <Text variant="bodySmall" style={{ color: resultado.ok ? theme.colors.success : theme.colors.error, flex: 1 }}>
                            {resultado.message}
                        </Text>
                    </Surface>
                )}

                <Button
                    mode="outlined"
                    onPress={handleTestar}
                    loading={testando}
                    disabled={testando}
                    icon="wifi"
                    style={styles.testButton}
                >
                    Testar Conexão
                </Button>

                <Button
                    mode="contained"
                    onPress={handleSalvar}
                    disabled={testando}
                    icon="content-save"
                    style={styles.saveButton}
                    contentStyle={styles.saveButtonContent}
                >
                    SALVAR
                </Button>

                <Button
                    mode="outlined"
                    onPress={handleLimparBanco}
                    icon="database-remove"
                    textColor={theme.colors.error}
                    style={styles.resetButton}
                >
                    Limpar banco local e ressincronizar
                </Button>
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    content: { padding: theme.spacing.lg },
    iconContainer: { alignItems: 'center', marginBottom: theme.spacing.lg },
    hint: { color: theme.colors.textSecondary, textAlign: 'center', marginBottom: theme.spacing.lg, lineHeight: 22 },
    input: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    resultBox: {
        flexDirection: 'row', alignItems: 'center',
        padding: theme.spacing.md, borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.md, gap: theme.spacing.sm,
    },
    testButton: { marginBottom: theme.spacing.sm + 4, borderRadius: theme.borderRadius.sm },
    saveButton: { borderRadius: theme.borderRadius.sm, marginBottom: theme.spacing.sm + 4 },
    saveButtonContent: { paddingVertical: theme.spacing.xs },
    resetButton: { borderColor: theme.colors.error, borderRadius: theme.borderRadius.sm, marginTop: theme.spacing.sm },
});
