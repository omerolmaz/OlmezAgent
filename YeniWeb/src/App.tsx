import { Suspense, lazy, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import MainLayout from './components/layout/MainLayout';
import Login from './pages/Login';
import Devices from './pages/Devices';
import DeviceDetail from './pages/DeviceDetail';
import Desktop from './pages/Desktop';
import Terminal from './pages/Terminal';
import Files from './pages/Files';
import Reports from './pages/Reports';
import BulkOperations from './pages/BulkOperations';
import { useAuthStore } from './stores/authStore';
import { useTranslation } from './hooks/useTranslation';

const Dashboard = lazy(() => import('./pages/Dashboard'));
const Commands = lazy(() => import('./pages/Commands'));
const Sessions = lazy(() => import('./pages/Sessions'));
const Users = lazy(() => import('./pages/Users'));
const Security = lazy(() => import('./pages/Security'));
const Inventory = lazy(() => import('./pages/Inventory'));
const Settings = lazy(() => import('./pages/Settings'));
const License = lazy(() => import('./pages/License'));
const Deployment = lazy(() => import('./pages/Deployment'));

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuthStore();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

function SuspenseView({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  return (
    <Suspense fallback={<div className="p-6 text-sm text-muted-foreground">{t('common.loading')}</div>}>
      {children}
    </Suspense>
  );
}

function App() {
  const { initialize } = useAuthStore();

  useEffect(() => {
    initialize();
  }, [initialize]);

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />

        <Route
          path="/"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Dashboard />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/dashboard"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Dashboard />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices"
          element={
            <PrivateRoute>
              <MainLayout>
                <Devices />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id"
          element={
            <PrivateRoute>
              <MainLayout>
                <DeviceDetail />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id/desktop"
          element={
            <PrivateRoute>
              <MainLayout>
                <Desktop />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id/terminal"
          element={
            <PrivateRoute>
              <MainLayout>
                <Terminal />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id/files"
          element={
            <PrivateRoute>
              <MainLayout>
                <Files />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id/commands"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Commands />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/commands"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Commands />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/sessions"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Sessions />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/devices/:id/sessions"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Sessions />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/users"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Users />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/inventory"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Inventory />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/security"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Security />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/reports"
          element={
            <PrivateRoute>
              <MainLayout>
                <Reports />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/bulk-operations/*"
          element={
            <PrivateRoute>
              <MainLayout>
                <BulkOperations />
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/settings"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Settings />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/license"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <License />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route
          path="/deployment"
          element={
            <PrivateRoute>
              <MainLayout>
                <SuspenseView>
                  <Deployment />
                </SuspenseView>
              </MainLayout>
            </PrivateRoute>
          }
        />

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
