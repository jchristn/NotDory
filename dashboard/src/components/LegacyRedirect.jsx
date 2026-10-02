import { Navigate, useLocation } from 'react-router-dom';

/**
 * Replace-redirect a pre-hub route to its hub + tab, keeping the original query string
 * (and hash). The target tab wins over any `tab` param on the old URL.
 */
function LegacyRedirect({ to, tab }) {
  const location = useLocation();
  const params = new URLSearchParams({ tab });
  new URLSearchParams(location.search).forEach((value, key) => {
    if (key !== 'tab') params.append(key, value);
  });
  return <Navigate to={`${to}?${params.toString()}${location.hash || ''}`} replace />;
}

export default LegacyRedirect;
