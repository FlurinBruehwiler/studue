package ch.studue.auth;

import java.util.Set;

public final class AuthorizationService {
    private static final Set<String> ALLOWED_LOGINS = Set.of(
            "bruehflu",
            "tomasma3",
            "muralsha",
            "colluc01",
            "gebhanoe",
            "srithaha",
            "erzinyan",
            "woehrlen",
            "merkenr1",
            "walthleo",
            "juhnkmor",
            "stixsim1",
            "ilguealp",
            "thiyath1",
            "meierc06",
            "vivekvin",
            "schsve01",
            "usluari1",
            "fazzaale",
            "arteaadr",
            "schaeal5",
            "neberjor",
            "lucasjoa",
            "piracyan",
            "bueloyan",
            "schnesev",
            "bischdav",
            "hasmoh01",
            "cotugsim",
            "rueegm01",
            "schoeli1",
            "orijelar",
            "smrqaalb",
            "maurefa3",
            "imeriane",
            "jaenneli",
            "kalluleo",
            "ciardmic",
            "jokicjov",
            "bamerleo",
            "stricrap",
            "penavant",
            "leeden02",
            "wettsol1"
    );

    public boolean isAllowedEditor(String githubLogin) {
        return githubLogin != null && ALLOWED_LOGINS.contains(githubLogin);
    }
}
