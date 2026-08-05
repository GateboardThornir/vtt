/**
 * English strings, namespaced by feature.
 *
 * Keys are cheap to add and expensive to rename, so the shape matters more than the wording.
 *
 * Deliberately not `as const`: the Italian set is typed against this one to catch a key added here
 * and forgotten there, and literal types would demand the Italian say the English words.
 */
export const en = {
  common: {
    appName: 'VTT',
    signOut: 'Sign out',
    loading: 'Loading…',
    language: 'Language',
    unexpectedError: 'Something went wrong. Please try again.',
  },
  signIn: {
    title: 'Sign in',
    username: 'Username',
    password: 'Password',
    submit: 'Sign in',
    invalidCredentials: 'That username and password do not match.',
    accountDisabled: 'This account has been disabled. Ask an administrator.',
  },
  pending: {
    title: 'Waiting for approval',
    body: 'Your account exists, but an administrator has not approved it yet. You will be able to sign in once they do.',
  },
  register: {
    title: 'Create your account',
    intro: 'You have been invited. Choose a username and a password.',
    username: 'Username',
    password: 'Password',
    submit: 'Create account',
    missingToken: 'This link is missing its invitation. Ask whoever invited you for a new one.',
    success: 'Your account has been created and is waiting for an administrator to approve it.',
    errors: {
      invite_invalid: 'This invitation is not valid. Ask for a new one.',
      invite_expired: 'This invitation has expired. Ask for a new one.',
      invite_already_used: 'This invitation has already been used.',
      username_taken: 'That username is taken.',
      username_invalid:
        'A username is 3 to 32 characters, using letters, digits, hyphen and underscore.',
      password_too_short: 'A password must be at least 12 characters.',
    },
  },
  admin: {
    title: 'Accounts',
    pendingTitle: 'Waiting for approval',
    noPending: 'Nobody is waiting.',
    approve: 'Approve',
    reject: 'Reject',
    disable: 'Disable',
    enable: 'Re-enable',
    username: 'Username',
    state: 'State',
    role: 'Role',
    registered: 'Registered',
    states: {
      Pending: 'Pending',
      Active: 'Active',
      Disabled: 'Disabled',
    },
    roles: {
      Member: 'Member',
      Admin: 'Administrator',
    },
  },
  home: {
    signedInAs: 'Signed in as {{username}}',
    adminLink: 'Manage accounts',
  },
}
