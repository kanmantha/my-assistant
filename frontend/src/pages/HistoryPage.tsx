import { useMemo, useState } from "react";
import { MessageSquare, Trash2 } from "lucide-react";
import { conversationsApi } from "../api/endpoints";
import { useApi } from "../hooks/useApi";
import { useSettings } from "../contexts/SettingsContext";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card, Button, EmptyState, Modal, Badge, Spinner } from "../components/ui";
import { t, languageName, dateLocale } from "../utils/locale";

type Sort = "newest" | "oldest" | "voice";

export function HistoryPage() {
  const { data, loading, error, setData } = useApi(() => conversationsApi.list());
  const { settings } = useSettings();
  const lang = settings.language;
  const [sort, setSort] = useState<Sort>("newest");
  const [confirmClear, setConfirmClear] = useState(false);
  const [clearing, setClearing] = useState(false);

  const conversations = useMemo(() => {
    const items = (data ?? []).slice();
    if (sort === "voice") return items.filter((c) => c.isVoice);
    items.sort((a, b) => (sort === "newest" ? +new Date(b.createdAt) - +new Date(a.createdAt) : +new Date(a.createdAt) - +new Date(b.createdAt)));
    return items;
  }, [data, sort]);

  const handleClear = async () => {
    setClearing(true);
    try {
      await conversationsApi.clear();
      setData(() => []);
      setConfirmClear(false);
    } finally {
      setClearing(false);
    }
  };

  return (
    <PageShell
      title={t("conversationHistory", lang)}
      actions={
        conversations.length > 0 ? (
          <Button variant="danger" onClick={() => setConfirmClear(true)}>
            <Trash2 className="h-4 w-4" /> {t("clearAll", lang)}
          </Button>
        ) : undefined
      }
    >
      {error && <ErrorBanner message={error} />}

      <div className="mb-4 flex max-w-2xs items-center gap-3">
        <span className="text-sm font-medium text-slate-500">{t("sortField", lang)}</span>
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value as Sort)}
          className="input w-auto"
          aria-label={t("sortAria", lang)}
        >
          <option value="newest">{t("newestFirst", lang)}</option>
          <option value="oldest">{t("oldestFirst", lang)}</option>
          <option value="voice">{t("voiceOnly", lang)}</option>
        </select>
      </div>

      {!data && loading ? (
        <LoadingBlock label={t("loading", lang)} />
      ) : conversations.length === 0 ? (
        <Card>
          <EmptyState
            icon={<MessageSquare className="h-7 w-7" />}
            title={t("noConversationsYet", lang)}
            hint={t("historyHint", lang)}
          />
        </Card>
      ) : (
        <div className="space-y-3">
          {conversations.map((c) => (
            <Card key={c.id} className="p-4">
              <div className="mb-1 flex items-start justify-between gap-2">
                <span className="chip bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">{languageName(c.language)}</span>
                <div className="flex items-center gap-2">
                  {c.intent && <Badge color="slate">{c.intent}</Badge>}
                  {c.isVoice && <Badge color="blue">{t("voiceBadge", lang)}</Badge>}
                  <span className="text-xs text-slate-400">{new Date(c.createdAt).toLocaleString(dateLocale(lang))}</span>
                </div>
              </div>
              <div className="mb-2 max-w-xs">
                <span className="text-xs font-semibold text-emerald-600 dark:text-emerald-400">{t("youLabel", lang)}:</span>
                <p className="text-sm text-slate-700 dark:text-slate-200">{c.userMessage}</p>
              </div>
              <div>
                <span className="text-xs font-semibold text-brand-600 dark:text-brand-400">{t("assistant", lang)}:</span>
                <p className="text-sm text-slate-600 dark:text-slate-300">{c.assistantResponse}</p>
              </div>
            </Card>
          ))}
        </div>
      )}

      <Modal open={confirmClear} onClose={() => setConfirmClear(false)} title={t("clearHistoryTitle", lang)}>
        <p className="mb-4 text-sm text-slate-500">{t("clearHistoryConfirm", lang)}</p>
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={() => setConfirmClear(false)}>{t("cancel", lang)}</Button>
          <Button variant="danger" onClick={() => void handleClear()} disabled={clearing}>
            {clearing && <Spinner />} {t("clearAll", lang)}
          </Button>
        </div>
      </Modal>
    </PageShell>
  );
}