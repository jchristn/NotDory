import ScopesView from '../ScopesView';
import MemoryBrowserView from '../MemoryBrowserView';
import InstructionsView from '../InstructionsView';
import SearchExplorerView from '../SearchExplorerView';
import ChatView from '../ChatView';
import EmbeddingEndpointsView from '../EmbeddingEndpointsView';
import InferenceEndpointsView from '../InferenceEndpointsView';
import TenantsView from '../TenantsView';
import UsersView from '../UsersView';
import CredentialsView from '../CredentialsView';
import RequestHistoryView from '../RequestHistoryView';
import OperationsView from '../OperationsView';
import ApiExplorerView from '../ApiExplorerView';
import SettingsView from '../SettingsView';
import AgentProtocolView from '../AgentProtocolView';
import CollectionsView from '../CollectionsView';

/**
 * Tab panel renderers per hub, keyed by the hub and tab keys in config/navConfig.jsx.
 * Labels, ordering, and gating live in the nav config; this only maps keys to views.
 */
const HUB_PANELS = {
  memory: {
    scopes: () => <ScopesView />,
    memories: () => <MemoryBrowserView />,
    instructions: () => <InstructionsView />
  },
  recall: {
    search: () => <SearchExplorerView />,
    chat: () => <ChatView />
  },
  models: {
    embedding: () => <EmbeddingEndpointsView />,
    inference: () => <InferenceEndpointsView />
  },
  access: {
    tenants: () => <TenantsView />,
    users: () => <UsersView />,
    credentials: () => <CredentialsView />
  },
  monitoring: {
    requests: () => <RequestHistoryView />,
    operations: () => <OperationsView />,
    'api-explorer': () => <ApiExplorerView />
  },
  system: {
    settings: () => <SettingsView />,
    'agent-onboarding': () => <AgentProtocolView />,
    collections: () => <CollectionsView />
  }
};

export default HUB_PANELS;
