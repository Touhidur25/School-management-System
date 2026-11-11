import { Component } from "react";

/** Last line of defence: an unexpected bug shows a message instead of a blank page. */
export default class ErrorBoundary extends Component {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error) {
    console.error(error);
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="notice notice-error" role="alert">
        <p>পাতাটি দেখাতে গিয়ে একটি সমস্যা হয়েছে।</p>
        <button type="button" onClick={() => window.location.reload()}>
          পাতাটি আবার লোড করুন
        </button>
      </div>
    );
  }
}
