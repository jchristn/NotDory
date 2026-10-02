import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useApp } from '../context/AppContext';
import { NAV_SECTIONS, itemVisible, itemIsActive } from '../config/navConfig';

/**
 * Workflow-ordered navigation rendered from config/navConfig.jsx: Workspace (organize and
 * recall memory) then Administration. Items gated to admins are hidden for other users
 * (client-side gating is UX only; the server still enforces authorization).
 */
function Sidebar() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const { isAdmin, isTenantAdmin } = useAuth();
  const { sidebarCollapsed } = useApp();
  const auth = { isAdmin, isTenantAdmin };

  const sections = NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => itemVisible(item, auth))
  })).filter((section) => section.items.length > 0);

  return (
    <aside className={`sidebar${sidebarCollapsed ? ' collapsed' : ''}`}>
      <div className="sidebar-brand">
        <img src="/logo.png" alt="NotDory" />
        <div className="brand-text">
          <span className="brand-name">{t('app.name')}</span>
          <span className="brand-tag">{t('app.tagline')}</span>
        </div>
      </div>
      <nav className="sidebar-nav" aria-label="Primary">
        {sections.map((section) => (
          <div className="nav-group" key={section.key}>
            <div className="nav-group-label">{t(section.labelKey)}</div>
            {section.items.map((item) => {
              const Icon = item.icon;
              const label = t(item.labelKey);
              const active = itemIsActive(item, pathname);
              return (
                <Link
                  key={item.key}
                  to={item.path}
                  className={`nav-item${active ? ' active' : ''}`}
                  aria-current={active ? 'page' : undefined}
                  title={label}
                >
                  <span className="nav-icon">
                    <Icon />
                  </span>
                  <span className="nav-label">{label}</span>
                </Link>
              );
            })}
          </div>
        ))}
      </nav>
      <div className="sidebar-footer">
        <span className="nav-label">NotDory 0.1.0 · ALPHA</span>
      </div>
    </aside>
  );
}

export default Sidebar;
