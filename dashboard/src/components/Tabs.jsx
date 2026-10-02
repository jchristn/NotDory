import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

/**
 * URL-synced tabbed surface. The active tab lives in the query string (default `?tab=`),
 * so bookmarks, deep links, and cross-page click-throughs can land on a specific tab.
 * Only the active tab's panel is mounted (lazy render). Other query params are preserved
 * when switching tabs. Hidden tabs are filtered out; an unknown or hidden `?tab=` value
 * falls back to the default (or first visible) tab.
 *
 * tabs: [{ key, label, render: () => ReactNode, hidden?: bool }]
 */
function Tabs({ tabs, param = 'tab', defaultTabKey, actions, ariaLabel }) {
  const [searchParams, setSearchParams] = useSearchParams();

  const visibleTabs = useMemo(() => (tabs || []).filter((tab) => !tab.hidden), [tabs]);

  const fallbackKey =
    defaultTabKey && visibleTabs.some((tab) => tab.key === defaultTabKey) ? defaultTabKey : visibleTabs[0]?.key;

  const requestedKey = searchParams.get(param);
  const activeKey = requestedKey && visibleTabs.some((tab) => tab.key === requestedKey) ? requestedKey : fallbackKey;

  const selectTab = useCallback(
    (key) => {
      const next = new URLSearchParams(searchParams);
      next.set(param, key);
      setSearchParams(next);
    },
    [param, searchParams, setSearchParams]
  );

  const onKeyDown = useCallback(
    (event) => {
      if (visibleTabs.length === 0) return;
      const currentIndex = visibleTabs.findIndex((tab) => tab.key === activeKey);
      let nextIndex = -1;
      if (event.key === 'ArrowRight' || event.key === 'ArrowDown') nextIndex = (currentIndex + 1) % visibleTabs.length;
      else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp')
        nextIndex = (currentIndex - 1 + visibleTabs.length) % visibleTabs.length;
      else if (event.key === 'Home') nextIndex = 0;
      else if (event.key === 'End') nextIndex = visibleTabs.length - 1;
      if (nextIndex >= 0) {
        event.preventDefault();
        const key = visibleTabs[nextIndex].key;
        selectTab(key);
        // Keep keyboard focus on the newly selected tab (roving tabindex).
        const el = document.getElementById(`tab-${param}-${key}`);
        if (el) el.focus();
      }
    },
    [activeKey, param, selectTab, visibleTabs]
  );

  const activeTab = visibleTabs.find((tab) => tab.key === activeKey);

  return (
    <div className="page-tabs">
      <div className="page-tabs-bar">
        <div className="page-tabs-list" role="tablist" aria-label={ariaLabel} onKeyDown={onKeyDown}>
          {visibleTabs.map((tab) => {
            const selected = tab.key === activeKey;
            return (
              <button
                key={tab.key}
                type="button"
                role="tab"
                id={`tab-${param}-${tab.key}`}
                aria-selected={selected}
                aria-controls={`tabpanel-${param}-${tab.key}`}
                tabIndex={selected ? 0 : -1}
                className={`page-tab${selected ? ' active' : ''}`}
                onClick={() => selectTab(tab.key)}
              >
                {tab.label}
              </button>
            );
          })}
        </div>
        {actions && <div className="page-tabs-actions">{actions}</div>}
      </div>
      {activeTab && (
        <div
          role="tabpanel"
          id={`tabpanel-${param}-${activeTab.key}`}
          aria-labelledby={`tab-${param}-${activeTab.key}`}
          className="page-tab-panel"
        >
          {activeTab.render()}
        </div>
      )}
    </div>
  );
}

export default Tabs;
