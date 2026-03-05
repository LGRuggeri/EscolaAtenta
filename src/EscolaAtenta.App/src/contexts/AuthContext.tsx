import React, { createContext, useState, useEffect, ReactNode } from 'react';
import { UsuarioLogado } from '../types/dtos';
import { PapelUsuario } from '../types/enums';
import { authStorage } from '../services/api';
import { authService } from '../services/authService';
import { jwtDecode, JwtPayload } from 'jwt-decode';

interface EscolaAtentaJwtPayload extends JwtPayload {
    email?: string;
    role?: string | number;
    name?: string;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'?: string;
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | number;
}

// Função para transformar a string "Administrador" no Enum correspondente, ou usar fallback
function parseRole(roleStringOrId: string | number): number {
    if (typeof roleStringOrId === 'number') return roleStringOrId;
    if (!isNaN(Number(roleStringOrId))) return Number(roleStringOrId);

    switch (roleStringOrId.toLowerCase()) {
        case 'administrador': return PapelUsuario.Administrador;
        case 'supervisao': return PapelUsuario.Supervisao;
        case 'monitor': return PapelUsuario.Monitor;
        default: return PapelUsuario.Monitor; // Fallback
    }
}
interface AuthContextData {
    signed: boolean;
    user: UsuarioLogado | null;
    loading: boolean;
    signIn: (email: string, senha: string) => Promise<void>;
    signOut: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextData>({} as AuthContextData);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [user, setUser] = useState<UsuarioLogado | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        async function loadStorageData() {
            const token = await authStorage.getToken();

            if (token) {
                try {
                    // Extrai os dados básicos do token para restaurar a sessão sem bater na API
                    const decoded = jwtDecode<EscolaAtentaJwtPayload>(token);

                    // O email pode vir do custom claim ou do padrão Microsoft
                    const emailClaim = decoded.email || decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';
                    const roleClaim = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role || 1;

                    setUser({
                        id: decoded.sub || '',
                        email: emailClaim,
                        nome: decoded.name || emailClaim.split('@')[0] || 'Usuário',
                        papel: parseRole(roleClaim)
                    });
                } catch (error) {
                    console.error("Token inválido ao carregar sessão:", error);
                    await authStorage.removeToken();
                }
            }
            setLoading(false);
        }

        loadStorageData();
    }, []);

    async function signIn(email: string, senha: string) {
        const response = await authService.login(email, senha);
        await authStorage.saveToken(response.token);

        // O backend C# retorna um LoginResponse direto: { token, email, papel, expiresAt }
        // Precisamos decodificar o token para pegar o ID real e o Nome se quisermos preencher o UsuarioLogado completo,
        // mas podemos usar os dados que vieram do response por agora.
        try {
            const decoded = jwtDecode<EscolaAtentaJwtPayload>(response.token);
            setUser({
                id: decoded.sub || '',
                email: response.email,
                nome: decoded.name || response.email.split('@')[0] || 'Usuário',
                papel: parseRole(response.papel || decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 1)
            });
        } catch (e) {
            console.error("Falha ao decodificar token no signIn", e);
        }
    }

    async function signOut() {
        await authStorage.removeToken();
        setUser(null);
    }

    return (
        <AuthContext.Provider value={{ signed: !!user, user, loading, signIn, signOut }}>
            {children}
        </AuthContext.Provider>
    );
};
