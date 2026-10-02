import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import { ThemeProvider } from './context/ThemeContext';
import { AppProvider } from './context/AppContext';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import ToastStack from './components/Toast';
import HomeView from './views/HomeView';
import ScopeDetail from './views/ScopeDetail';
import CategoriesView from './views/CategoriesView';
import MemoriesView from './views/MemoriesView';
import ChatView from './views/ChatView';
import HubView from './views/hubs/HubView';
import LegacyRedirect from './components/LegacyRedirect';
import { NAV_ITEMS, LEGACY_REDIRECTS, DASHBOARD_BASE } from './config/navConfig';
import './App.css';

function PrivateRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();
  if (isLoading) return <div className="app-loading"><div className="spinner" /></div>;
  return isAuthenticated ? children : <Navigate to="/" replace />;
}

function PublicRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();
  if (isLoading) return <div className="app-loading"><div className="spinner" /></div>;
  return !isAuthenticated ? children : <Navigate to="/dashboard/home" replace />;
}

function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <PublicRoute>
            <Login />
          </PublicRoute>
        }
      />
      <Route
        path="/dashboard"
        element={
          <PrivateRoute>
            <Dashboard />
          </PrivateRoute>
        }
      >
        <Route index element={<Navigate to="home" replace />} />
        <Route path="home" element={<HomeView />} />
        {NAV_ITEMS.filter((item) => item.tabs).map((item) => (
          <Route key={item.key} path={item.path.slice(DASHBOARD_BASE.length + 1)} element={<HubView key={item.key} hubKey={item.key} />} />
        ))}
        <Route path="scopes/:scopeId" element={<ScopeDetail />} />
        <Route path="scopes/:scopeId/categories" element={<CategoriesView />} />
        <Route path="scopes/:scopeId/memories" element={<MemoriesView />} />
        <Route path="scopes/:scopeId/chat" element={<ChatView />} />
        {/* Pre-hub URLs redirect (replace) to their hub + tab, preserving the query string. */}
        {LEGACY_REDIRECTS.map((r) => (
          <Route key={r.from} path={r.from} element={<LegacyRedirect to={r.to} tab={r.tab} />} />
        ))}
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <AppProvider>
          <BrowserRouter>
            <AppRoutes />
            <ToastStack />
          </BrowserRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
