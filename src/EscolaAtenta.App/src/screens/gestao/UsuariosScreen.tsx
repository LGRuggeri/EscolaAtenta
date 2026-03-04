import React, { useState, useCallback, useRef, useEffect } from 'react';
import {
    View,
    Text,
    StyleSheet,
    FlatList,
    ActivityIndicator,
    TouchableOpacity,
    TextInput,
    Alert,
} from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { usuariosService, UsuarioDto } from '../../services/usuariosService';
import { theme } from '../../theme/colors';

// ── Constantes ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 20;
const DEBOUNCE_MS = 500;

// ── Helpers ──────────────────────────────────────────────────────────────────

function getPapelBadge(papel: string): { label: string; bg: string; text: string } {
    switch (papel) {
        case 'Administrador':
            return { label: 'Admin', bg: theme.colors.primary, text: theme.colors.surface };
        case 'Supervisao':
            return { label: 'Supervisão', bg: theme.colors.secondaryLight, text: theme.colors.primaryDark };
        case 'Monitor':
            return { label: 'Monitor', bg: theme.colors.secondary, text: theme.colors.surface };
        default:
            return { label: papel, bg: theme.colors.border, text: theme.colors.textPrimary };
    }
}

// ── Componente Principal ─────────────────────────────────────────────────────

export function UsuariosScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    // ── Estado: dados e paginação ────────────────────────────────────────────
    const [usuarios, setUsuarios] = useState<UsuarioDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);

    // ── Estado: busca ────────────────────────────────────────────────────────
    const [searchQuery, setSearchQuery] = useState('');

    // ── Refs de controle ─────────────────────────────────────────────────────
    const isFetchingRef = useRef(false);
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const searchQueryRef = useRef(searchQuery);

    useEffect(() => { searchQueryRef.current = searchQuery; }, [searchQuery]);

    // ── Fetch centralizado ───────────────────────────────────────────────────
    const fetchUsuarios = useCallback(async (
        page: number,
        term: string,
        append: boolean,
    ) => {
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

    // ── Recarrega ao ganhar foco (pós-criação) ──────────────────────────────
    useFocusEffect(
        useCallback(() => {
            fetchUsuarios(1, searchQueryRef.current, false);
        }, [fetchUsuarios])
    );

    // ── Debounce na busca ────────────────────────────────────────────────────
    const handleSearchChange = useCallback((texto: string) => {
        setSearchQuery(texto);
        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        debounceTimer.current = setTimeout(() => {
            setUsuarios([]);
            setCurrentPage(1);
            fetchUsuarios(1, texto, false);
        }, DEBOUNCE_MS);
    }, [fetchUsuarios]);

    // ── Infinite Scroll ──────────────────────────────────────────────────────
    const handleEndReached = useCallback(() => {
        if (!hasNextPage || isFetchingRef.current || loading) return;
        fetchUsuarios(currentPage + 1, searchQueryRef.current, true);
    }, [hasNextPage, currentPage, loading, fetchUsuarios]);

    // ── Alternar status (soft delete) ────────────────────────────────────────
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
                            // Atualiza estado local sem reload completo
                            setUsuarios(prev =>
                                prev.map(u =>
                                    u.id === usuario.id ? { ...u, ativo: !u.ativo } : u
                                )
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

    // ── Render de item ───────────────────────────────────────────────────────
    const renderItem = useCallback(({ item }: { item: UsuarioDto }) => {
        const badge = getPapelBadge(item.papel);

        return (
            <View style={[styles.card, !item.ativo && styles.cardInativo]}>
                <View style={styles.cardTop}>
                    <View style={{ flex: 1 }}>
                        <Text style={[styles.cardNome, !item.ativo && styles.textInativo]} numberOfLines={1}>
                            {item.nome}
                        </Text>
                        <Text style={styles.cardEmail} numberOfLines={1}>{item.email}</Text>
                    </View>
                    <View style={[styles.papelBadge, { backgroundColor: badge.bg }]}>
                        <Text style={[styles.papelBadgeText, { color: badge.text }]}>{badge.label}</Text>
                    </View>
                </View>
                <View style={styles.cardBottom}>
                    <View style={[styles.statusBadge, item.ativo ? styles.statusAtivo : styles.statusInativo]}>
                        <Text style={[styles.statusText, item.ativo ? styles.statusTextoAtivo : styles.statusTextoInativo]}>
                            {item.ativo ? 'Ativo' : 'Inativo'}
                        </Text>
                    </View>
                    <View style={styles.cardActions}>
                        <TouchableOpacity
                            style={styles.actionButton}
                            onPress={() => navigation.navigate('UsuarioForm', { id: item.id })}
                        >
                            <Text style={styles.actionButtonText}>Editar</Text>
                        </TouchableOpacity>
                        <TouchableOpacity
                            style={[styles.actionButton, item.ativo ? styles.actionButtonDanger : styles.actionButtonSuccess]}
                            onPress={() => handleAlternarStatus(item)}
                        >
                            <Text style={[styles.actionButtonText, item.ativo ? styles.actionTextDanger : styles.actionTextSuccess]}>
                                {item.ativo ? 'Bloquear' : 'Desbloquear'}
                            </Text>
                        </TouchableOpacity>
                    </View>
                </View>
            </View>
        );
    }, [navigation, handleAlternarStatus]);

    // ── Footer spinner ───────────────────────────────────────────────────────
    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" color={theme.colors.primary} />
                <Text style={styles.footerLoaderText}>Carregando mais...</Text>
            </View>
        );
    };

    // ── Empty State ──────────────────────────────────────────────────────────
    const renderListaVazia = () => {
        if (loading) return null;
        return (
            <View style={styles.emptyContainer}>
                <Text style={styles.emptyIcon}>👥</Text>
                <Text style={styles.emptyText}>
                    {searchQuery.trim()
                        ? `Nenhum usuário encontrado para "${searchQuery}".`
                        : 'Nenhum usuário cadastrado.'}
                </Text>
            </View>
        );
    };

    // ── Render ───────────────────────────────────────────────────────────────
    return (
        <SafeAreaView style={styles.container}>
            {/* Header */}
            <View style={styles.header}>
                <View style={styles.headerLeft}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                        <Text style={styles.backButtonText}>← Voltar</Text>
                    </TouchableOpacity>
                    <Text style={styles.headerTitle}>Usuários</Text>
                </View>
                <TouchableOpacity
                    style={styles.addButton}
                    onPress={() => navigation.navigate('UsuarioForm')}
                >
                    <Text style={styles.addButtonText}>+ Novo</Text>
                </TouchableOpacity>
            </View>

            {/* Barra de busca */}
            <View style={styles.searchContainer}>
                <View style={styles.searchBox}>
                    <Text style={styles.searchIcon}>🔍</Text>
                    <TextInput
                        style={styles.searchInput}
                        placeholder="Buscar por nome ou e-mail..."
                        placeholderTextColor={theme.colors.textSecondary}
                        value={searchQuery}
                        onChangeText={handleSearchChange}
                        returnKeyType="search"
                        autoCorrect={false}
                        autoCapitalize="none"
                    />
                    {searchQuery.length > 0 && (
                        <TouchableOpacity onPress={() => handleSearchChange('')}>
                            <Text style={styles.searchClear}>✕</Text>
                        </TouchableOpacity>
                    )}
                </View>
            </View>

            {/* Lista */}
            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" color={theme.colors.primary} />
                    <Text style={styles.loadingText}>Carregando usuários...</Text>
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
                    ListEmptyComponent={renderListaVazia}
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

// ── Estilos ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },

    // Header
    header: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
        padding: 20, paddingTop: 20, backgroundColor: theme.colors.surface,
        elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2,
    },
    headerLeft: { flexDirection: 'row', alignItems: 'center' },
    backButton: { marginRight: 12 },
    backButtonText: { fontSize: 16, color: theme.colors.primary, fontWeight: '600' },
    headerTitle: { fontSize: 24, fontWeight: 'bold', color: theme.colors.textPrimary },
    addButton: {
        backgroundColor: theme.colors.primary, paddingHorizontal: 16,
        paddingVertical: 8, borderRadius: 20,
    },
    addButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 14 },

    // Busca
    searchContainer: {
        backgroundColor: theme.colors.surface, paddingHorizontal: 16,
        paddingTop: 12, paddingBottom: 12,
        borderBottomWidth: 1, borderBottomColor: theme.colors.border,
    },
    searchBox: {
        flexDirection: 'row', alignItems: 'center',
        backgroundColor: theme.colors.background, borderRadius: 10,
        borderWidth: 1, borderColor: theme.colors.border,
        paddingHorizontal: 12, height: 44, gap: 8,
    },
    searchIcon: { fontSize: 16 },
    searchInput: {
        flex: 1, fontSize: 15, color: theme.colors.textPrimary, paddingVertical: 0,
    },
    searchClear: { fontSize: 18, color: theme.colors.textSecondary, paddingHorizontal: 4 },

    // Loading
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { marginTop: 12, color: theme.colors.textSecondary, fontSize: 14 },

    // Footer
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: 16, gap: 8,
    },
    footerLoaderText: { fontSize: 13, color: theme.colors.textSecondary },

    // Lista
    listContainer: { paddingHorizontal: 16, paddingBottom: 40, paddingTop: 12 },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },

    // Card
    card: {
        backgroundColor: theme.colors.surface, padding: 16, borderRadius: 12,
        marginBottom: 10, elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.08, shadowRadius: 3,
    },
    cardInativo: { opacity: 0.6 },
    cardTop: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'flex-start', gap: 12,
    },
    cardNome: { fontSize: 16, fontWeight: 'bold', color: theme.colors.textPrimary },
    textInativo: { textDecorationLine: 'line-through' },
    cardEmail: { fontSize: 13, color: theme.colors.textSecondary, marginTop: 2 },
    cardBottom: { flexDirection: 'row', alignItems: 'center', marginTop: 10, gap: 8 },

    // Badge Papel
    papelBadge: {
        paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20,
        flexShrink: 0,
    },
    papelBadgeText: { fontSize: 11, fontWeight: '700' },

    // Badge Status
    statusBadge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20 },
    statusAtivo: { backgroundColor: theme.colors.secondary },
    statusInativo: { backgroundColor: theme.colors.border },
    statusText: { fontSize: 11, fontWeight: '700' },
    statusTextoAtivo: { color: theme.colors.surface },
    statusTextoInativo: { color: theme.colors.textSecondary },

    // Ações do card
    cardActions: {
        flexDirection: 'row', marginLeft: 'auto', gap: 8,
    },
    actionButton: {
        paddingHorizontal: 12, paddingVertical: 6, borderRadius: 16,
        borderWidth: 1, borderColor: theme.colors.primary,
    },
    actionButtonText: {
        fontSize: 12, fontWeight: '700', color: theme.colors.primary,
    },
    actionButtonDanger: { borderColor: theme.colors.error },
    actionTextDanger: { color: theme.colors.error },
    actionButtonSuccess: { borderColor: theme.colors.secondary },
    actionTextSuccess: { color: theme.colors.secondary },

    // Empty
    emptyContainer: { alignItems: 'center', paddingVertical: 48 },
    emptyIcon: { fontSize: 48, marginBottom: 16 },
    emptyText: { fontSize: 16, fontWeight: '600', color: theme.colors.textSecondary, textAlign: 'center' },
});
