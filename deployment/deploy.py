"""Provision the capped game backend. No provider keys are ever written under Assets.

Run: python deployment/deploy.py --project PROJECT --billing-account ACCOUNT
Paid AI stays disabled unless --enable-ai is explicitly supplied after credit review.
"""
import argparse
import base64
import json
import pathlib
import secrets
import shutil
import subprocess
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
GCLOUD = shutil.which('gcloud.cmd') or shutil.which('gcloud')
ACCOUNT = 'pushkal.vashist@amztech.ai'
REGION = 'asia-southeast1'
SERVICE = 'nyp-game-ai'


def cli(*args, capture=False):
    result = subprocess.run([GCLOUD, *args, '--account=' + ACCOUNT, '--quiet'],
                            text=True, capture_output=capture, check=True)
    return result.stdout.strip() if capture else None


def api(url, method='GET', data=None):
    token = cli('auth', 'print-access-token', capture=True)
    request = urllib.request.Request(url, method=method,
        headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json',
                 'x-goog-user-project': ARGS.project},
        data=json.dumps(data).encode() if data is not None else None)
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            body = response.read()
            return json.loads(body) if body else {}
    except urllib.error.HTTPError as error:
        # Google API errors do not include request Authorization or secret payloads.
        raise RuntimeError(f'{method} {url}: {error.code} {error.read().decode()}') from None


def secret(name, initial_value):
    url = f'https://secretmanager.googleapis.com/v1/projects/{ARGS.project}/secrets'
    existing = api(url).get('secrets', [])
    full_name = f'projects/{ARGS.project}/secrets/{name}'
    if not any(s['name'].endswith('/' + name) for s in existing):
        api(url + '?secretId=' + name, 'POST', {'replication': {'automatic': {}}})
    versions = api('https://secretmanager.googleapis.com/v1/' + full_name + '/versions').get('versions', [])
    if not any(v['state'] == 'ENABLED' for v in versions):
        api('https://secretmanager.googleapis.com/v1/' + full_name + ':addVersion', 'POST',
            {'payload': {'data': base64.b64encode(initial_value.encode()).decode()}})
    return full_name


def budgets(project_number):
    url = f'https://billingbudgets.googleapis.com/v1/billingAccounts/{ARGS.billing_account}/budgets'
    existing = api(url).get('budgets', [])
    # SGD billing account. Gross costs exclude credits so credit usage remains visible.
    for label, service, amount in [('Vertex', 'C7E2-9256-1C43', '100'), ('Cloud Run', '152E-C115-5142', '15')]:
        name = f'NYP 2027 {label} spend cap'
        if any(b.get('displayName') == name for b in existing):
            print(name + ': already configured', flush=True)
            continue
        budget = {
            'displayName': name,
            'budgetFilter': {'projects': ['projects/' + project_number], 'services': ['services/' + service],
                             'calendarPeriod': 'MONTH', 'creditTypesTreatment': 'EXCLUDE_ALL_CREDITS'},
            'amount': {'specifiedAmount': {'currencyCode': 'SGD', 'units': amount}},
            'thresholdRules': [{'thresholdPercent': p, 'spendBasis': 'CURRENT_SPEND'} for p in [0.5, 0.8, 1.0]],
            'notificationsRule': {'enableProjectLevelRecipients': True, 'disableDefaultIamRecipients': False},
            'spendCap': {'inputState': 'CONFIGURED'},
        }
        created = api(url, 'POST', budget)
        print(f'{name}: SGD {amount}/month; state={created.get("spendCap", {}).get("outputState")}', flush=True)


def main():
    project = ARGS.project
    project_number = cli('projects', 'describe', project, '--format=value(projectNumber)', capture=True)
    print('Provisioning', project, flush=True)
    cli('services', 'enable', 'run.googleapis.com', 'aiplatform.googleapis.com', 'firestore.googleapis.com',
        'secretmanager.googleapis.com', 'cloudbuild.googleapis.com', 'artifactregistry.googleapis.com',
        'billingbudgets.googleapis.com', '--project=' + project)
    databases = json.loads(cli('firestore', 'databases', 'list', '--project=' + project, '--format=json', capture=True))
    if not databases:
        cli('firestore', 'databases', 'create', '--database=(default)', '--location=' + REGION,
            '--type=firestore-native', '--project=' + project)
    elif not any(d['name'].endswith('/(default)') and d['type'] == 'FIRESTORE_NATIVE' for d in databases):
        raise RuntimeError('Project has an incompatible database; choose a database explicitly before deploying.')

    runtime = f'{SERVICE}@{project}.iam.gserviceaccount.com'
    accounts = json.loads(cli('iam', 'service-accounts', 'list', '--project=' + project, '--format=json', capture=True))
    if not any(a['email'] == runtime for a in accounts):
        cli('iam', 'service-accounts', 'create', SERVICE, '--display-name=NYP game runtime', '--project=' + project)
    for role in ['roles/aiplatform.user', 'roles/datastore.user']:
        cli('projects', 'add-iam-policy-binding', project, '--member=serviceAccount:' + runtime,
            '--role=' + role, '--condition=None', '--format=none')

    keys_path = ROOT / 'deployment/device-keys.local.json'
    if not keys_path.exists():
        keys_path.write_text(json.dumps({f'kiosk{i:02}': secrets.token_urlsafe(32) for i in range(1, 21)}, indent=2))
    secret('nyp-game-device-keys', keys_path.read_text())
    secret('nyp-game-openrouter-key', 'NOT_CONFIGURED')
    for name in ['nyp-game-device-keys', 'nyp-game-openrouter-key']:
        cli('secrets', 'add-iam-policy-binding', name, '--project=' + project,
            '--member=serviceAccount:' + runtime, '--role=roles/secretmanager.secretAccessor', '--format=none')
    budgets(project_number)

    repos = json.loads(cli('artifacts', 'repositories', 'list', '--location=' + REGION,
                          '--project=' + project, '--format=json', capture=True))
    if not any(r['name'].endswith('/nyp-game') for r in repos):
        cli('artifacts', 'repositories', 'create', 'nyp-game', '--repository-format=docker',
            '--location=' + REGION, '--project=' + project)
    builder = cli('builds', 'get-default-service-account', '--project=' + project, capture=True).rsplit('/', 1)[-1]
    buckets = cli('storage', 'buckets', 'list', '--project=' + project, '--format=value(name)', capture=True).splitlines()
    if f'{project}_cloudbuild' not in buckets and f'gs://{project}_cloudbuild' not in buckets:
        cli('storage', 'buckets', 'create', f'gs://{project}_cloudbuild', '--location=' + REGION, '--project=' + project,
            '--uniform-bucket-level-access')
    cli('storage', 'buckets', 'add-iam-policy-binding', f'gs://{project}_cloudbuild',
        '--member=serviceAccount:' + builder, '--role=roles/storage.objectViewer', '--format=none')
    cli('artifacts', 'repositories', 'add-iam-policy-binding', 'nyp-game', '--location=' + REGION,
        '--project=' + project, '--member=serviceAccount:' + builder,
        '--role=roles/artifactregistry.writer', '--format=none')
    cli('projects', 'add-iam-policy-binding', project, '--member=serviceAccount:' + builder,
        '--role=roles/logging.logWriter', '--condition=None', '--format=none')
    # No source from the Unity repository is uploaded: build context is backend/ only.
    image = f'{REGION}-docker.pkg.dev/{project}/nyp-game/ai:latest'
    cli('builds', 'submit', str(ROOT / 'backend'), '--config=' + str(ROOT / 'backend/cloudbuild.yaml'),
        '--substitutions=_IMAGE=' + image, '--project=' + project, '--timeout=600s')
    env_file = ROOT / 'deployment/run-env.local.yaml'
    env_file.write_text('\n'.join([
        f'GOOGLE_CLOUD_PROJECT: "{project}"', 'VERTEX_MODEL: "gemini-3.5-flash-lite"',
        f'PAID_AI_ENABLED: "{str(ARGS.enable_ai).lower()}"',
        'OPENROUTER_MODEL: "nvidia/nemotron-3.5-lightning:free"', 'ALLOWED_ORIGINS: ""',
    ]) + '\n')
    cli('run', 'deploy', SERVICE, '--project=' + project, '--region=' + REGION, '--image=' + image,
        '--service-account=' + runtime, '--allow-unauthenticated', '--ingress=all',
        '--cpu=1', '--memory=256Mi', '--concurrency=20', '--min=0', '--max=2',
        '--cpu-throttling', '--timeout=40s', '--env-vars-file=' + str(env_file),
        '--set-secrets=GAME_DEVICE_KEYS=nyp-game-device-keys:latest,OPENROUTER_API_KEY=nyp-game-openrouter-key:latest')
    url = cli('run', 'services', 'describe', SERVICE, '--project=' + project, '--region=' + REGION,
              '--format=value(status.url)', capture=True)
    keys = json.loads(keys_path.read_text())
    config_dir = ROOT / 'deployment/kiosk-configs.local.d'
    config_dir.mkdir(exist_ok=True)
    for device, key in keys.items():
        (config_dir / (device + '.json')).write_text(json.dumps({'backendUrl': url, 'gameKey': key}, indent=2))
    user_settings = ROOT / 'UserSettings'
    user_settings.mkdir(exist_ok=True)
    (user_settings / 'nyp-ai-config.json').write_text(json.dumps({'backendUrl': url, 'gameKey': keys['kiosk01']}, indent=2))
    (ROOT / 'deployment/status.local.json').write_text(json.dumps({
        'project': project, 'url': url, 'paidAiEnabled': ARGS.enable_ai,
    }, indent=2))
    print('Backend URL:', url, flush=True)
    print('Unity Editor uses kiosk01; per-device configs saved outside Assets.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--billing-account', required=True)
    parser.add_argument('--enable-ai', action='store_true')
    ARGS = parser.parse_args()
    main()
