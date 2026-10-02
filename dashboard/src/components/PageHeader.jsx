import { useHub } from '../context/HubContext';

/**
 * Route header: optional breadcrumbs, title, subtitle, and action slot.
 * Inside a hub tab (see HubView) the hub already renders the page title, so the header
 * collapses to a compact row with just the subtitle and actions.
 */
function PageHeader({ title, subtitle, actions, breadcrumbs }) {
  const { embedded } = useHub();

  if (embedded) {
    if (!subtitle && !actions) return null;
    return (
      <div className="page-header embedded">
        <div>{subtitle && <p className="page-subtitle">{subtitle}</p>}</div>
        {actions && <div className="page-actions">{actions}</div>}
      </div>
    );
  }

  return (
    <div className="section">
      {breadcrumbs && <div className="breadcrumbs">{breadcrumbs}</div>}
      <div className="page-header">
        <div>
          <h1>{title}</h1>
          {subtitle && <p className="page-subtitle">{subtitle}</p>}
        </div>
        {actions && <div className="page-actions">{actions}</div>}
      </div>
    </div>
  );
}

export default PageHeader;
