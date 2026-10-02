import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useApp } from '../context/AppContext';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';
import { LoadingState } from '../components/States';

// Everything NotDory sends a model when an agent connects: the MCP server instructions (placed in the model's system prompt
// by agent harnesses, and repeated by session_start) and every tool description. A system administrator can edit each;
// blank or default text falls back to the built-in version. The MCP server picks up a save within its refresh interval.
function AgentProtocolView() {
  const { t } = useTranslation();
  const { apiClient, isAdmin } = useAuth();
  const { addToast } = useApp();

  const [protocol, setProtocol] = useState(null);
  const [instructions, setInstructions] = useState('');
  const [descriptions, setDescriptions] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const apply = useCallback((data) => {
    setProtocol(data);
    setInstructions(data.serverInstructions || '');
    const next = {};
    (data.tools || []).forEach((tool) => {
      next[tool.name] = tool.description || '';
    });
    setDescriptions(next);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      apply(await apiClient.getAgentProtocol());
    } catch (e) {
      addToast(e.message || String(e), 'error');
    } finally {
      setLoading(false);
    }
  }, [apiClient, apply, addToast]);

  useEffect(() => {
    load();
  }, [load]);

  const save = async () => {
    setSaving(true);
    try {
      // Only text that differs from the default is stored as an override.
      const toolDescriptions = {};
      (protocol?.tools || []).forEach((tool) => {
        const value = (descriptions[tool.name] || '').trim();
        if (value && value !== tool.defaultDescription) toolDescriptions[tool.name] = value;
      });
      const text = instructions.trim();
      const body = { serverInstructions: text && text !== protocol.defaultServerInstructions ? text : null, toolDescriptions };
      apply(await apiClient.updateAgentProtocol(body));
      addToast(t('agentProtocol.saved'), 'success');
    } catch (e) {
      addToast(e.message || String(e), 'error');
    } finally {
      setSaving(false);
    }
  };

  const changed =
    protocol &&
    (instructions !== (protocol.serverInstructions || '') ||
      (protocol.tools || []).some((tool) => (descriptions[tool.name] || '') !== (tool.description || '')));

  return (
    <>
      <PageHeader
        title={t('agentProtocol.title')}
        subtitle={t('agentProtocol.subtitle')}
        actions={
          <>
            <button className="btn-secondary" onClick={load} disabled={saving}>
              {t('common.refresh')}
            </button>
            {isAdmin && (
              <button className="btn-primary" onClick={save} disabled={saving || !changed}>
                {saving ? t('agentProtocol.saving') : t('agentProtocol.save')}
              </button>
            )}
          </>
        }
      />

      {loading || !protocol ? (
        <LoadingState />
      ) : (
        <>
          {!isAdmin && <div className="section card field-hint">{t('agentProtocol.readOnly')}</div>}

          <div className="section card">
            <div className="section-title">
              {t('agentProtocol.instructions')}{' '}
              {protocol.serverInstructionsOverridden ? (
                <StatusBadge tone="warning">{t('agentProtocol.edited')}</StatusBadge>
              ) : (
                <StatusBadge tone="neutral">{t('agentProtocol.default')}</StatusBadge>
              )}
            </div>
            <div className="field">
              <textarea value={instructions} onChange={(e) => setInstructions(e.target.value)} rows={12} readOnly={!isAdmin} />
              <span className="field-hint">{t('agentProtocol.instructionsHint')}</span>
            </div>
            {isAdmin && instructions !== protocol.defaultServerInstructions && (
              <button className="btn-secondary" onClick={() => setInstructions(protocol.defaultServerInstructions)}>
                {t('agentProtocol.reset')}
              </button>
            )}
          </div>

          <div className="section card">
            <div className="section-title">{t('agentProtocol.tools')}</div>
            <span className="field-hint">{t('agentProtocol.toolsHint')}</span>
            {(protocol.tools || []).map((tool) => {
              const value = descriptions[tool.name] || '';
              const isDefault = value === tool.defaultDescription;
              return (
                <div className="field" key={tool.name} style={{ marginTop: 'var(--spacing-md)' }}>
                  <label>
                    <code>{tool.name}</code>{' '}
                    {!isDefault && <StatusBadge tone="warning">{t('agentProtocol.edited')}</StatusBadge>}
                  </label>
                  <textarea
                    value={value}
                    onChange={(e) => setDescriptions({ ...descriptions, [tool.name]: e.target.value })}
                    rows={3}
                    readOnly={!isAdmin}
                  />
                  {isAdmin && !isDefault && (
                    <button className="btn-secondary" onClick={() => setDescriptions({ ...descriptions, [tool.name]: tool.defaultDescription })}>
                      {t('agentProtocol.reset')}
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}

export default AgentProtocolView;
