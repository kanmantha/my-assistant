import { useState, type FormEvent } from "react";
import { Search as SearchIcon, Loader2 } from "lucide-react";
import { searchApi } from "../api/endpoints";
import { useSettings } from "../contexts/SettingsContext";
import { PageShell } from "../components/PageShell";
import { Card, Input, Button, Badge, EmptyState } from "../components/ui";
import type { SearchResponse, SearchResultItem } from "../models";
import { t } from "../utils/locale";

const SCOPE_VALUES = ["notes", "tasks", "appointments", "reminders"];

export function SearchPage() {
  const { settings } = useSettings();
  const lang = settings.language;
  const [query, setQuery] = useState("");
  const [scope, setScope] = useState<string>("all");
  const [results, setResults] = useState<SearchResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searched, setSearched] = useState(false);

  const SCOPE_LABELS: Record<string, string> = {
    all: t("all", lang),
    notes: t("notes", lang),
    tasks: t("tasks", lang),
    appointments: t("appointments", lang),
    reminders: t("reminders", lang)
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;
    setLoading(true);
    setError(null);
    setSearched(true);
    try {
      const scopes = scope === "all" ? undefined : [scope];
      const res = await searchApi.search({ query, scopes });
      setResults(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("searchFailed", lang));
    } finally {
      setLoading(false);
    }
  };

  const typeLabel = (type: string): string => {
    switch (type) {
      case "note":
        return t("notes", lang);
      case "task":
        return t("tasks", lang);
      case "appointment":
        return t("appointments", lang);
      case "reminder":
        return t("reminders", lang);
      default:
        return type;
    }
  };

  const typeBadge: Record<string, string> = {
    note: "blue",
    task: "amber",
    appointment: "green",
    reminder: "red"
  };

  return (
    <PageShell title={t("search", lang)}>
      <form onSubmit={(e) => void handleSubmit(e)} className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("searchPlaceholder", lang)}
            className="pl-9"
          />
        </div>
        <div className="flex items-center gap-2">
          <select value={scope} onChange={(e) => setScope(e.target.value)} className="input w-auto" aria-label={t("searchScopeAria", lang)}>
            {["all", ...SCOPE_VALUES].map((v) => (
              <option key={v} value={v}>{SCOPE_LABELS[v]}</option>
            ))}
          </select>
          <Button type="submit" loading={loading}>
            {t("search", lang)}
          </Button>
        </div>
      </form>

      {error && <p className="mb-4 text-sm text-rose-600">{error}</p>}

      {!searched ? (
        <Card>
          <EmptyState
            icon={<span className="h-7 w-7">🔍</span>}
            title={t("searchWorkspaceTitle", lang)}
            hint={t("searchWorkspaceHint", lang)}
          />
        </Card>
      ) : loading ? (
        <div className="flex items-center justify-center py-16 text-slate-400">
          <Loader2 className="mr-2 h-5 w-5 animate-spin" /> {t("searching", lang)}
        </div>
      ) : !results || flattenSearch(results).length === 0 ? (
        <Card>
          <EmptyState icon={<SearchIcon className="h-7 w-7" />} title={t("noResultsTitle", lang)} hint={t("noResultsHint", lang)} />
        </Card>
      ) : (
        <div className="space-y-2">
          <p className="text-sm text-slate-500">{flattenSearch(results).length} {t("searchResultsLabel", lang)}</p>
          {flattenSearch(results).map((item, i) => (
            <Card key={`${item.type}-${item.id}-${i}`} className="flex items-center justify-between gap-3 p-3.5">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <Badge color={typeBadge[item.type] ?? "slate"}>{typeLabel(item.type)}</Badge>
                  <p className="font-medium text-slate-800 dark:text-slate-100">{item.title}</p>
                </div>
                {item.snippet && <p className="mt-1 line-clamp-1 text-xs text-slate-500">{item.snippet}</p>}
                {item.date && <p className="mt-0.5 text-xs text-slate-400">{item.date}</p>}
              </div>
            </Card>
          ))}
        </div>
      )}
    </PageShell>
  );
}

function flattenSearch(res: SearchResponse): SearchResultItem[] {
  return [
    ...res.notes.map((r) => ({ ...r, type: "note" })),
    ...res.tasks.map((r) => ({ ...r, type: "task" })),
    ...res.appointments.map((r) => ({ ...r, type: "appointment" })),
    ...res.reminders.map((r) => ({ ...r, type: "reminder" }))
  ];
}