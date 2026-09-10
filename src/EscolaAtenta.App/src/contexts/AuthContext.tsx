import React, { createContext, useState, useEffect, useRef, ReactNode } from 'react';
import { UsuarioLogado } from '../types/dtos';
import { PapelUsuario } from '../types/enums';
import { api, authStorage, loadServerUrl, setSessionHandlers } from '../services/api';
import { authService } from '../services/authService';
import { jwtDecode, JwtPayload } from 'jwt-decode';
import { activateDatabase, deactivateDatabase } from '../database';
import { sessionScope } from '../services/sessionScope';

interface EscolaAtentaJwtPayload extends JwtPayload {
    email?: string;
    role?: string | number;
    name?: string;
    deve_alterar_senha?: string | boolean;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'?: string;
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | number;
}
function parseRole(role: string | number): number {
    if (typeof role === 'number' || !isNaN(Number(role))) return Number(role);
    switch (role.toLowerCase()) {
        case 'administrador': return PapelUsuario.Administrador;
        case 'supervisao': return PapelUsuario.Supervisao;
        default: return PapelUsuario.Monitor;
    }
}
interface AuthContextData {
    signed: boolean;
    user: UsuarioLogado | null;
    loading: boolean;
    deveAlterarSenha: boolean;
    signIn: (email: string, senha: string) => Promise<void>;
    signOut: () => Promise<void>;
}
export const AuthContext = createContext<AuthContextData>({} as AuthContextData);
export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [user, setUser] = useState<UsuarioLogado | null>(null);
    const [loading, setLoading] = useState(true);
    const [deveAlterarSenha, setDeveAlterarSenha] = useState(false);
    const transitioning = useRef(false);

    async function activate(token: string, passwordRequired?: boolean) {
        const decoded = jwtDecode<EscolaAtentaJwtPayload>(token);
        if (!decoded.sub || !api.defaults.baseURL) throw new Error('Sessão sem identidade.');
        await activateDatabase(api.defaults.baseURL, decoded.sub);
        sessionScope.active = true;
        const email = decoded.email || decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';
        setDeveAlterarSenha(passwordRequired === true || decoded.deve_alterar_senha === true || decoded.deve_alterar_senha === 'true');
        setUser({ id: decoded.sub, email, nome: decoded.name || email.split('@')[0] || 'Usuário',
            papel: parseRole(decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role || 1) });
    }
    useEffect(() => {
        setSessionHandlers(() => { void signOut(); }, () => setDeveAlterarSenha(true));
        void (async () => {
            try {
                await loadServerUrl();
                const token = await authStorage.getToken();
                if (token) await activate(token);
            } catch {
                await authStorage.removeToken();
                deactivateDatabase();
            } finally { setLoading(false); }
        })();
        return () => setSessionHandlers();
    }, []);

    async function signIn(email: string, senha: string) {
        if (transitioning.current) throw new Error('Aguarde a troca de sessão.');
        transitioning.current = true;
        try {
            await sessionScope.invalidate();
            await authStorage.removeToken();
            const response = await authService.login(email, senha);
            await authStorage.saveToken(response.token);
            if (response.refreshToken) await authStorage.saveRefreshToken(response.refreshToken);
            await activate(response.token, response.deveAlterarSenha);
        } catch (error) {
            await authStorage.removeToken();
            deactivateDatabase();
            setUser(null);
            throw error;
        } finally { transitioning.current = false; }
    }
    async function signOut() {
        if (transitioning.current) return;
        transitioning.current = true;
        setLoading(true);
        setUser(null);
        setDeveAlterarSenha(false);
        try {
            await sessionScope.invalidate();
            await authStorage.removeToken();
            deactivateDatabase();
        } finally {
            transitioning.current = false;
            setLoading(false);
        }
    }
    return <AuthContext.Provider value={{ signed: !!user, user, loading, deveAlterarSenha, signIn, signOut }}>{children}</AuthContext.Provider>;
};
