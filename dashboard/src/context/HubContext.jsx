import { createContext, useContext } from 'react';

/**
 * Set by `HubView` around the active tab panel. Views rendered inside a hub tab read it
 * (via PageHeader) to drop their own heavy page title, since the hub already shows one.
 */
const HubContext = createContext({ embedded: false });

export const HubProvider = HubContext.Provider;

export function useHub() {
  return useContext(HubContext);
}

export default HubContext;
