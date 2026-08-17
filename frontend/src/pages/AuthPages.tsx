import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Sparkles, Languages } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useSettings } from "../contexts/SettingsContext";
import { authApi } from "../api/endpoints";
import { Input, Field, Spinner, Select } from "../components/ui";
import { t } from "../utils/locale";

export type AuthMode = "login" | "register" | "forgot" | "reset";

const TITLE_KEYS: Record<AuthMode, string> = {
  login: "authLoginTitle",
  register: "authSignUpTitle",
  forgot: "authForgotTitle",
  reset: "authResetTitle"
};

const SUBTITLE_KEYS: Record<AuthMode, string> = {
  login: "authLoginSub",
  register: "authSignUpSub",
  forgot: "authForgotSub",
  reset: "authResetSub"
};

function AuthCard({ children, title, subtitle, lang }: { children: React.ReactNode; title: string; subtitle: string; lang: string }) {
  const { setLocal } = useSettings();
  return (
    <div className="min-h-screen bg-gradient-to-br from-brand-50 via-slate-50 to-sky-50 py-10 dark:from-slate-950 dark:via-slate-900 dark:to-brand-950">
      <div className="mx-auto flex w-full max-w-md flex-col px-4">
        <div className="mb-4 flex items-center justify-between">
          <div className="flex items-center justify-center gap-2 text-brand-700 dark:text-brand-300">
            <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-lg">
              <Sparkles className="h-6 w-6" />
            </span>
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
            <Languages className="h-4 w-4" />
            <Select
              value={lang}
              onChange={(e) => setLocal({ language: e.target.value })}
              className="w-auto"
              aria-label="Language"
            >
              <option value="en">English</option>
              <option value="hi">हिंदी</option>
              <option value="te">తెలుగు</option>
            </Select>
          </label>
        </div>
        <div className="glass-card p-6 sm:p-8">
          <h1 className="mb-1 text-2xl font-bold text-slate-800 dark:text-slate-100">{title}</h1>
          <p className="mb-6 text-sm text-slate-500">{subtitle}</p>
          {children}
        </div>
      </div>
    </div>
  );
}

export function AuthPage({ mode }: { mode: AuthMode }) {
  const { login, register } = useAuth();
  const { settings } = useSettings();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState(params.get("email") ?? "");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [token, setToken] = useState(params.get("token") ?? "");
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const lang = settings.language;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setLoading(true);
    try {
      if (mode === "login") {
        await login(email, password);
        navigate("/");
      } else if (mode === "register") {
        if (password.length < 8) throw new Error(t("pwMinError", lang));
        if (password !== confirm) throw new Error(t("pwMatchError", lang));
        await register(firstName, lastName, email, password);
        navigate("/");
      } else if (mode === "forgot") {
        await authApi.forgotPassword(email);
        setInfo(t("forgotSent", lang));
      } else if (mode === "reset") {
        if (newPassword.length < 8) throw new Error(t("pwMinError", lang));
        if (newPassword !== confirm) throw new Error(t("pwMatchError", lang));
        await authApi.resetPassword({ email, token, newPassword });
        setInfo(t("resetSuccess", lang));
        setTimeout(() => navigate("/login"), 1500);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t("genericError", lang));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthCard title={t(TITLE_KEYS[mode], lang)} subtitle={t(SUBTITLE_KEYS[mode], lang)} lang={lang}>
      <form className="space-y-4" onSubmit={handleSubmit}>
        {mode === "register" && (
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("firstName", lang)}>
              <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required autoComplete="given-name" />
            </Field>
            <Field label={t("lastName", lang)}>
              <Input value={lastName} onChange={(e) => setLastName(e.target.value)} required autoComplete="family-name" />
            </Field>
          </div>
        )}

        <Field label={t("email", lang)}>
          <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" placeholder="you@example.com" />
        </Field>

        {mode === "login" && (
          <Field label={t("passwordField", lang)}>
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" />
          </Field>
        )}

        {(mode === "register" || mode === "reset") && (mode === "reset" ? (
          <>
            <Field label={t("newPasswordField", lang)}>
              <Input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required autoComplete="new-password" />
            </Field>
            <Field label={t("confirmNewPassword", lang)}>
              <Input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required autoComplete="new-password" />
            </Field>
          </>
        ) : (
          <>
            <Field label={t("passwordMin", lang)}>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="new-password" />
            </Field>
            <Field label={t("confirmPassword", lang)}>
              <Input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required autoComplete="new-password" />
            </Field>
          </>
        ))}

        {mode === "reset" && (
          <Field label={t("resetToken", lang)}>
            <Input value={token} onChange={(e) => setToken(e.target.value)} required />
          </Field>
        )}

        {error && <p className="text-sm text-rose-600 dark:text-rose-400" role="alert">{error}</p>}
        {info && <p className="text-sm text-emerald-600 dark:text-emerald-400">{info}</p>}

        <button type="submit" disabled={loading} className="btn-primary w-full">
          {loading ? <Spinner /> : mode === "login" ? t("signIn", lang) : mode === "register" ? t("createAccount", lang) : mode === "forgot" ? t("sendResetLink", lang) : t("updatePassword", lang)}
        </button>
      </form>

      <div className="mt-6 space-y-2 text-center text-sm">
        {mode === "login" ? (
          <>
            <p className="text-slate-500">
              {t("noAccount", lang)}{" "}
              <Link to="/register" className="font-semibold text-brand-600 dark:text-brand-300">
                {t("signUp", lang)}
              </Link>
            </p>
            <Link to="/forgot-password" className="text-slate-400 hover:text-brand-500">
              {t("forgotPassword", lang)}
            </Link>
          </>
        ) : mode === "register" ? (
          <p className="text-slate-500">
            {t("haveAccount", lang)}{" "}
            <Link to="/login" className="font-semibold text-brand-600 dark:text-brand-300">
              {t("signIn", lang)}
            </Link>
          </p>
        ) : (
          <Link to="/login" className="text-slate-400 hover:text-brand-500">
            {t("backToLogin", lang)}
          </Link>
        )}
      </div>
    </AuthCard>
  );
}