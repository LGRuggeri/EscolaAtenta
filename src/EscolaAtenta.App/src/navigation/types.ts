import { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { TurmaDto, AlunoDto } from '../types/dtos';

export type RootStackParamList = {
    Login: undefined;
    ConfiguracaoServidor: undefined;
    Home: undefined;
    Turmas: undefined;
    TurmaForm: { turma?: TurmaDto };
    Alunos: { turmaId: string; turmaNome: string };
    AlunoForm: { turmaId: string; aluno?: AlunoDto };
    ChamadaOperacao: { turmaId: string; turmaNome: string };
    Usuarios: undefined;
    UsuarioForm: { id?: string } | undefined;
    Alertas: undefined;
    HistoricoAlertas: undefined;
    RelatorioPresencas: undefined;
    TrocarSenha: undefined;
};

export type AppNavigationProp = NativeStackNavigationProp<RootStackParamList>;
