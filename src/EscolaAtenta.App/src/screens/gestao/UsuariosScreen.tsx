import React, { useState, useCallback, useRef, useEffect } from 'react';
import { View, StyleSheet, FlatList, Alert } from 'react-native';
import { Text, Searchbar, ActivityIndicator, Card, Chip, IconButton, Button } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { usuariosService, UsuarioDto } from '../../services/usuariosService';
import { AppHeader, EmptyState, StatusChip } from '../../components/ui';
import { theme } from '../../theme/colors';

const PAGE_SIZE = 20;
const DEBOUNCE_MS = 500;

function getPapelConfig(papel: string): { variant: 'info' | 'success' | 'warning' | 'error' | 'neutral'; label: string } {
    switch (papel) {
        case 'Administrador': return { variant: 'info', label: 'Admin' };
        case 'Supervisao': return { variant: 'warning', label: 'Supervisão' };
        case 'Monitor': return { variant: 'success', label: 'Monitor' };
        default: return { variant: 'neutral', label: papel };
    }
}

export function UsuariosScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    const [usuarios, setUsuarios] = useState<UsuarioDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);
    const [searchQuery, setSearchQuery] = useState('');

    const isFetchingRef = useRef(false);
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const searchQueryRef = useRef(searchQuery);

    useEffect(() => { searchQueryRef.current = searchQuery; }, [searchQuery]);

    const fetchUsuarios = useCallback(async (page: number, term: string, append: boolean) => {
        if (append && isFetchingRef.current) return;
        try {
            isFetchingRef.current = true;
            if (page === 1) setLoading(true);
            else setLoadingMore(true);

            const resultado = await usuariosService.getUsuarios({
                pageNumber: page,
                pageSize: PAGE_SIZE,
                ...(term.trim() ? { searchTerm: term.trim() } : {}),
            });

            setUsuarios(prev => append ? [...prev, ...resultado.items] : resultado.items);
            setCurrentPage(page);
            setHasNextPage(resultado.hasNextPage);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar os usuários.');
            console.error(error);
        } finally {
            setLoading(false);
            setLoadingMore(false);
            isFetchingRef.current = false;
        }
    }, []);

    useFocusEffect(
        useCallback(() => {
            fetchUsuarios(1, searchQueryRef.current, false);
        }, [fetchUsuarios])
    );

    const handleSearchChange = useCallback((texto: string) => {
        setSearchQuery(texto);
        if (debounceTimer.current) clearTimeout(debounceTimer.current);
        debounceTimer.current = setTimeout(() => {
            setUsuarios([]);
            setCurrentPage(1);
            fetchUsuarios(1, texto, false);
        }, DEBOUNCE_MS);
    }, [fetchUsuarios]);

    const handleEndReached = useCallback(() => {
        if (!hasNextPage || isFetchingRef.current || loading) return;
        fetchUsuarios(currentPage + 1, searchQueryRef.current, true);
    }, [hasNextPage, currentPage, loading, fetchUsuarios]);

    const handleAlternarStatus = useCallback((usuario: UsuarioDto) => {
        const acao = usuario.ativo ? 'desativar' : 'reativar';
        Alert.alert(
            'Confirmar',
            `Deseja realmente ${acao} o acesso de ${usuario.nome}?`,
            [
                { text: 'Cancelar', style: 'cancel' },
                {
                    text: usuario.ativo ? 'Desativar' : 'Reativar',
                    style: usuario.ativo ? 'destructive' : 'default',
                    onPress: async () => {
                        try {
                            await usuariosService.alternarStatusUsuario(usuario.id);
                            setUsuarios(prev =>
                                prev.map(u => u.id === usuario.id ? { ...u, ativo: !u.ativo } : u)
                            );
                        } catch (error) {
                            Alert.alert('Erro', `Não foi possível ${acao} o usuário.`);
                            console.error(error);
                        }
                    },
                },
            ],
        );
    }, []);

    const renderItem = useCallback(({ item }: { item: UsuarioDto }) => {
        const papelConfig = getPapelConfig(item.papel);

        return (
            <Card style={[styles.card, !item.ativo && styles.cardInativo]} mode="elevated">
                <Card.Content>
                    <View style={styles.cardTop}>
                        <View style={styles.cardInfo}>
                            <Text variant="titleMedium" style={[styles.cardNome, !item.ativo && styles.textInativo]}>
                                {item.nome}
                            </Text>
                            <Text variant="bodySmall" style={styles.cardEmail}>{item.email}</Text>
                        </View>
                        <StatusChip label={papelConfig.label} variant={papelConfig.variant} />
                    </View>
                    <View style={styles.cardBottom}>
                        <Chip
                            compact
                            icon={item.ativo ? 'check-circle' : 'close-circle'}
                            textStyle={{ fontSize: 11, fontWeight: '700', color: item.ativo ? theme.colors.success : theme.colors.textSecondary }}
                            style={{ backgroundColor: item.ativo ? theme.colors.successLight : theme.colors.surfaceVariant }}
                        >
                            {item.ativo ? 'Ativo' : 'Inativo'}
                        </Chip>
                        <View style={styles.cardActions}>
                            <IconButton
                                icon="pencil-outline"
                                size={18}
                                onPress={() => navigation.navigate('UsuarioForm', { id: item.id })}
                            />
                            <IconButton
                                icon={item.ativo ? 'account-lock' : 'account-check'}
                                size={18}
                                iconColor={item.ativo ? theme.colors.error : theme.colors.success}
                                onPress={() => handleAlternarStatus(item)}
                            />
                        </View>
                    </View>
                </Card.Content>
            </Card>
        );
    }, [navigation, handleAlternarStatus]);

    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" />
                <Text variant="labelSmall" style={styles.footerText}>Carregando mais...</Text>
            </View>
        );
    };

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Usuários"
                onBack={() => navigation.goBack()}
                rightActions={[{ icon: 'account-plus', onPress: () => navigation.navigate('UsuarioForm'), label: 'Novo usuário' }]}
            />

            <Searchbar
                placeholder="Buscar por nome ou e-mail..."
                value={searchQuery}
                onChangeText={handleSearchChange}
                style={styles.searchBar}
                inputStyle={styles.searchInput}
            />

            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                    <Text variant="bodyMedium" style={styles.loadingText}>Carregando usuários...</Text>
                </View>
            ) : (
                <FlatList
                    data={usuarios}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={[
                        styles.listContainer,
                        usuarios.length === 0 && styles.listContainerEmpty,
                    ]}
                    ListEmptyComponent={
                        <EmptyState
                            icon="account-group"
                            title={searchQuery.trim() ? `Nenhum resultado para "${searchQuery}"` : 'Nenhum usuário cadastrado'}
                        />
                    }
                    ListFooterComponent={renderListFooter}
                    onEndReached={handleEndReached}
                    onEndReachedThreshold={0.5}
                    removeClippedSubviews
                    initialNumToRender={10}
                    maxToRenderPerBatch={10}
                    windowSize={5}
                />
            )}
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    searchBar: {
        margin: theme.spacing.md,
        marginBottom: 0,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
    },
    searchInput: { fontSize: 15 },
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { color: theme.colors.textSecondary, marginTop: theme.spacing.md },
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: theme.spacing.md, gap: theme.spacing.sm,
    },
    footerText: { color: theme.colors.textSecondary },
    listContainer: { padding: theme.spacing.md, paddingBottom: theme.spacing.xl },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },
    card: {
        marginBottom: theme.spacing.sm + 4,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
    },
    cardInativo: { opacity: 0.6 },
    cardTop: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'flex-start', gap: theme.spacing.sm,
    },
    cardInfo: { flex: 1 },
    cardNome: { fontWeight: 'bold', color: theme.colors.textPrimary },
    textInativo: { textDecorationLine: 'line-through' },
    cardEmail: { color: theme.colors.textSecondary, marginTop: 2 },
    cardBottom: {
        flexDirection: 'row', alignItems: 'center',
        marginTop: theme.spacing.sm, justifyContent: 'space-between',
    },
    cardActions: { flexDirection: 'row' },
});
