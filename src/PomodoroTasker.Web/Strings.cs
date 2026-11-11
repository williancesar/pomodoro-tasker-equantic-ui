namespace PomodoroTasker.Web;

/// <summary>Every interface string, in PT-BR. Code and comments stay in English.</summary>
public static class T
{
    public static string N(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    public const string App = "pomodoro-tasker";
    public const string NavToday = "Hoje", NavLists = "Listas", NavProjects = "Projetos", NavRoutines = "Rotinas",
        NavReports = "Relatórios", NavSettings = "Configurações";
    public const string Add = "Adicionar", Create = "Criar", Save = "Salvar", Cancel = "Cancelar", Delete = "Excluir",
        Rename = "Renomear", Restore = "Restaurar", Undo = "Desfazer", Close = "Fechar", More = "Mais ações",
        MoveUp = "Mover para cima", MoveDown = "Mover para baixo", Name = "Nome";

    // Today
    public const string TodayNewTask = "Nova tarefa para hoje";
    public static string TodayDoneOf(int done, int total) => $"{done} de {total} feitas";
    public static string TodayCarried(int c) => c == 1 ? "1 veio de outro dia" : $"{c} vieram de outros dias";
    public static string Tomatoes(int c) => N(c, "tomate", "tomates");
    public const string TodayEmptyTitle = "Nada planejado para hoje";
    public const string TodayEmptyText = "Escreva uma tarefa acima ou puxe tarefas dos seus projetos e listas. O que ficar aberto no fim do dia passa para amanhã.";
    public const string TodayPull = "Puxar do backlog";
    public const string TodayAllDoneTitle = "Dia cumprido";
    public const string TodayAllDoneText = "Nada ficou para amanhã. Você pode puxar mais tarefas.";

    // Task
    public const string TaskEdit = "Editar tarefa", TaskToBacklog = "Devolver ao backlog", TaskToToday = "Puxar para Hoje",
        TaskInToday = "Já está em Hoje. Clique para devolver ao backlog", TaskInTodayTag = "em Hoje",
        TaskMarkDone = "Marcar como feita", TaskMarkOpen = "Reabrir", TaskHigh = "alta", TaskLow = "baixa";
    public static string TaskSubs(int done, int total) => $"{done} de {total} sub-tarefas";
    public static string TaskPostponed(int c) => $"adiada {c}×";
    public static string TaskPlanned(string label) => $"planejada para {label}";
    public static string TaskLooseList(string name) => $"Lista avulsa · {name}";
    public const string TaskTitle = "Título", TaskNotes = "Notas", TaskList = "Lista", TaskNoList = "Sem lista",
        TaskPriority = "Prioridade", TaskPlannedDate = "Data planejada", TaskDeadline = "Prazo", TaskTags = "Tags",
        TaskTagsHint = "Separadas por vírgula.", TaskPomodoro = "Fazer em pomodoros",
        TaskPomodoroHint = "Liga sozinho quando você inicia um foco na tarefa.", TaskSubtasks = "Sub-tarefas",
        TaskNewSub = "Nova sub-tarefa", TaskTitleRequired = "Escreva um título para a tarefa.",
        TaskDeleteTitle = "Excluir esta tarefa?";
    public static readonly string[] TaskPriorities = ["Baixa", "Normal", "Alta"];
    public static string TaskDeleteText(string title, int subs) =>
        subs == 0 ? $"“{title}” será apagada." : $"“{title}” e {N(subs, "sub-tarefa", "sub-tarefas")} serão apagadas.";
    public const string DateNone = "Sem data", DateClear = "Limpar";

    // Lists
    public static string ListsActive(int c) => N(c, "ativa", "ativas");
    public const string ListsNewName = "Nome da nova lista";
    public static string ListsItems(int done, int total) => $"{done} de {total} itens";
    public static string InToday(int c) => $"{c} em Hoje";
    public const string ListsArchived = "Arquivadas";
    public static string ListsSeeArchived(int c) => c == 1 ? "ver a arquivada" : $"ver as {c}";
    public const string ListsArchivedHint = "Listas concluídas ficam aqui até você apagar.";
    public static string ListsArchivedOn(string label) => $"arquivada {label}";
    public static string ListsArchivedToast(string name) => $"“{name}” foi concluída e arquivada.";
    public const string ListsNewItem = "Novo item", ListsArchiveNow = "Arquivar agora", ListsReadOnly = "somente leitura",
        ListsEmptyTitle = "Nenhuma lista avulsa",
        ListsEmptyText = "Uma lista avulsa serve para coisas rápidas, fora de projetos. Ela se arquiva sozinha quando tudo é feito.",
        ListsNoArchived = "Nenhuma lista arquivada.", ListsDeleteTitle = "Excluir esta lista?";
    public static string ListsCount(int c) => N(c, "lista", "listas");
    public static string ListsDeleteText(string name, int total) => $"“{name}” e {N(total, "tarefa", "tarefas")} serão apagadas.";

    // Projects
    public static string ProjectsActive(int c) => N(c, "ativo", "ativos");
    public const string ProjectsNewName = "Nome do novo projeto";
    public static string ProjectsOpen(int c) => N(c, "aberta", "abertas");
    public static string ProjectsDone(int c) => N(c, "feita", "feitas");
    public static string ProjectsOverdue(int c) => $"{c} com prazo vencido";
    public const string ProjectsCompleted = "Concluídos";
    public static string ProjectsCompletedCount(int c) => N(c, "projeto", "projetos");
    public static string ProjectsCompletedOn(string label) => $"concluído {label}";
    public const string ProjectsMarkCompleted = "Marcar como concluído", ProjectsReopen = "Reabrir projeto",
        ProjectsCreateList = "Criar lista", ProjectsRenameList = "Renomear lista", ProjectsDeleteList = "Excluir lista",
        ProjectsEmptyTitle = "Este projeto ainda não tem listas",
        ProjectsEmptyText = "Uma lista agrupa tarefas de um mesmo assunto. Crie a primeira para começar.",
        ProjectsNoneTitle = "Nenhum projeto ainda",
        ProjectsNoneText = "Projetos guardam o backlog: cada um tem uma ou mais listas de tarefas.",
        ProjectsDeleteTitle = "Excluir este projeto?";
    public static string ProjectsNewTaskIn(string list) => $"Nova tarefa em {list}";
    public static string ProjectsDeleteText(string name, int lists, int tasks) =>
        $"“{name}” tem {N(lists, "lista", "listas")} e {N(tasks, "tarefa", "tarefas")}. Tudo isso é apagado.";

    // Timer
    public const string TimerIdle = "Nenhum foco", TimerIdleHint = "Escolha uma tarefa e inicie um pomodoro",
        TimerFocus = "Foco", TimerPaused = "Pausado", TimerShort = "Pausa curta", TimerLong = "Pausa longa",
        TimerBreakPaused = "pausada", TimerWait = "Pausa encerrada", TimerStart = "Iniciar", TimerPause = "Pausar",
        TimerResume = "Retomar", TimerStop = "Parar", TimerSkip = "Pular", TimerBusy = "Outro foco está em andamento",
        TimerStopTitle = "Parar este foco?", TimerReason = "Motivo (opcional)", TimerDoneReason = "Tarefa concluída",
        TimerKeepFocus = "Continuar foco", TimerStopFocus = "Parar foco";
    public static readonly string[] TimerReasons = ["Reunião", "Telefone", "Cansaço", "Outra urgência"];
    public static string TimerOf(int n, int total) => $"{n} de {total}";
    public static string TimerNext(string task) => $"Próximo: {task}";
    public static string TimerAfter(string task) => $"Depois: {task}";
    public static string TimerCycle(int n, int total) => $"{n} de {total} pomodoros neste ciclo";
    public static string TimerStartOn(string task) => $"Iniciar pomodoro: {task}";
    public static string TimerStopText(int minutes) =>
        $"{(minutes == 1 ? "Falta 1 minuto" : $"Faltam {minutes} minutos")}. Ele fica no histórico como interrompido e não vale tomate.";
    public static string TimerTomatoes(int done, int interrupted) =>
        $"{N(done, "tomate", "tomates")}{(interrupted > 0 ? $", {N(interrupted, "interrompido", "interrompidos")}" : "")}";

    // What the desktop app sends as a system notification is shown on the page.
    public const string NotifyDoneTitle = "Pomodoro completo", NotifyBreakTitle = "A pausa acabou";
    public static string NotifyDoneBody(string task, int minutes) => $"{task} ganhou um tomate. Pausa curta de {minutes} minutos.";
    public static string NotifyCycle(int every, int minutes) => $"Ciclo de {every} completo. Hora da pausa longa: {minutes} minutos.";
    public static string NotifyBreakBody(string task) => $"Próximo foco: {task}.";
}
