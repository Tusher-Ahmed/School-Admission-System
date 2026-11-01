import { Component, type ErrorInfo, type ReactNode } from 'react';

interface State {
  failed: boolean;
}

/** Last line of defence: an unexpected rendering error shows a message, never a blank page. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('UI error', error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="page">
        <div className="alert alert-error" role="alert">
          <p>দুঃখিত, পেজটি দেখাতে একটি সমস্যা হয়েছে।</p>
          <button type="button" onClick={() => window.location.reload()}>
            আবার লোড করুন
          </button>
        </div>
      </div>
    );
  }
}
