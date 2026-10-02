import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { HubProvider } from '../../context/HubContext';
import Tabs from '../../components/Tabs';
import { EmptyState } from '../../components/States';
import { findNavItem, gateVisible } from '../../config/navConfig';
import HUB_PANELS from './hubPanels';

const EMBEDDED = { embedded: true };

/**
 * A tabbed hub page: the hub title plus a URL-synced (?tab=) tab strip. Tabs, labels,
 * and gating come from config/navConfig.jsx; panels come from hubPanels.jsx. Tab views
 * keep their own internals but render a compact header (no duplicate page title).
 */
function HubView({ hubKey }) {
  const { t } = useTranslation();
  const { isAdmin, isTenantAdmin } = useAuth();
  const item = findNavItem(hubKey);

  const tabs = useMemo(() => {
    const panels = HUB_PANELS[hubKey] || {};
    return (item?.tabs || []).map((tab) => ({
      key: tab.key,
      label: t(tab.labelKey),
      hidden: !gateVisible(tab.gate || 'none', { isAdmin, isTenantAdmin }) || !panels[tab.key],
      render: () => <HubProvider value={EMBEDDED}>{panels[tab.key]()}</HubProvider>
    }));
  }, [hubKey, item, t, isAdmin, isTenantAdmin]);

  if (!item) return null;
  const title = t(item.labelKey);
  const anyVisible = tabs.some((tab) => !tab.hidden);

  return (
    <>
      <div className="page-header hub-header">
        <h1>{title}</h1>
      </div>
      {anyVisible ? <Tabs tabs={tabs} ariaLabel={title} /> : <EmptyState title={title} message={t('nav.noAccess')} />}
    </>
  );
}

export default HubView;
